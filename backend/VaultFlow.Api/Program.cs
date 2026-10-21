using System.Text.Json;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("GroqClient", client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

// Cadena de conexión obtenida de appsettings.json o Variables de Entorno en Azure Container Apps
string getConnectionString(IConfiguration config) =>
    config.GetConnectionString("DefaultConnection") ?? config["SqlConnectionString"] ?? "";

app.MapGet("/", () => Results.Ok(new { status = "Online", service = "VaultFlow API" }))
   .WithTags("VaultFlow.Api");

// -------------------------------------------------------------
// ENDPOINTS DE TRANSACCIONES (Conectados a Azure SQL)
// -------------------------------------------------------------

// GET /api/transactions - Lee desde Azure SQL
app.MapGet("/api/transactions", async (IConfiguration config) =>
{
    var list = new List<Transaction>();
    var connStr = getConnectionString(config);

    if (string.IsNullOrEmpty(connStr))
    {
        return Results.Problem("Cadena de conexión a Azure SQL no configurada.");
    }

    using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    var cmd = new SqlCommand("SELECT Id, Description, Amount, Category, Merchant FROM Transactions", conn);
    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        list.Add(new Transaction(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.IsDBNull(3) ? "General" : reader.GetString(3),
            reader.IsDBNull(4) ? "Desconocido" : reader.GetString(4)
        ));
    }

    return Results.Ok(list);
})
.WithName("GetTransactions")
.WithTags("Transactions");

// POST /api/transactions - Inserta en Azure SQL
app.MapPost("/api/transactions", async (IConfiguration config, TransactionInput input) =>
{
    var connStr = getConnectionString(config);
    if (string.IsNullOrEmpty(connStr))
    {
        return Results.Problem("Cadena de conexión a Azure SQL no configurada.");
    }

    using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    var sql = @"INSERT INTO Transactions (Description, Amount, Category, Merchant, Date) 
                OUTPUT INSERTED.Id 
                VALUES (@Desc, @Amount, @Cat, @Merch, GETDATE())";

    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@Desc", input.Description);
    cmd.Parameters.AddWithValue("@Amount", input.Amount);
    cmd.Parameters.AddWithValue("@Cat", string.IsNullOrEmpty(input.Category) ? "General" : input.Category);
    cmd.Parameters.AddWithValue("@Merch", string.IsNullOrEmpty(input.Merchant) ? "Desconocido" : input.Merchant);

    var newId = (int)await cmd.ExecuteScalarAsync();

    var newTx = new Transaction(newId, input.Description, input.Amount, input.Category, input.Merchant);
    return Results.Created($"/api/transactions/{newId}", newTx);
})
.WithName("CreateTransaction")
.WithTags("Transactions");

// -------------------------------------------------------------
// ENDPOINT DE AGENTE IA
// -------------------------------------------------------------

app.MapPost("/api/ai/chat", async (IHttpClientFactory httpClientFactory, IConfiguration config, ChatRequest request) =>
{
    try
    {
        var apiKey = config["GroqApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return Results.Json(new { error = "La variable GroqApiKey está vacía en la configuración." }, statusCode: 500);
        }

        var client = httpClientFactory.CreateClient("GroqClient");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        string contextData = "No hay transacciones registradas actualmente.";
        if (request.Transactions != null && request.Transactions.Any())
        {
            var txList = request.Transactions.Select(t => $"- {t.Description}: ${t.Amount} (Categoría: {t.Category}, Comercio: {t.Merchant})");
            contextData = string.Join("\n", txList);
        }

        var systemPrompt = $"Eres el Asistente Financiero IA de la plataforma VaultFlow.\n" +
                           $"Tienes acceso a las siguientes transacciones del usuario en tiempo real:\n" +
                           $"{contextData}\n\n" +
                           $"Responde las dudas del usuario basándote en esta información de manera clara, concisa y precisa.";

        var payload = new
        {
            model = "openai/gpt-oss-20b",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = request.Prompt }
            }
        };

        var response = await client.PostAsJsonAsync("chat/completions", payload);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new { error = $"Groq Error ({response.StatusCode}): {responseString}" }, statusCode: (int)response.StatusCode);
        }

        using var jsonDoc = JsonDocument.Parse(responseString);
        var reply = jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return Results.Ok(new { response = reply });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message, detail = ex.ToString() }, statusCode: 500);
    }
})
.WithName("AskAIAgent")
.WithTags("AI Agent");

app.Run();

public record Transaction(int Id, string Description, decimal Amount, string Category, string Merchant);
public record TransactionInput(string Description, decimal Amount, string Category, string Merchant);
public record ChatTransaction(string Description, decimal Amount, string Category, string Merchant);
public record ChatRequest(string Prompt, List<ChatTransaction>? Transactions);