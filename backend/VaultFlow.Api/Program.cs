using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configuración de servicios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cliente HTTP para Groq
builder.Services.AddHttpClient("GroqClient", client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
});

// Configuración de CORS
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

// Base de datos temporal / en memoria de transacciones
var transactions = new List<Transaction>
{
    new(1, "Suscripción Azure", 50.00m, "Licencia", "Microsoft"),
    new(2, "Suscripción Prime", 30.00m, "Streaming", "Amazon Prime"),
    new(3, "Suscripción Netflix", 40.00m, "Streaming", "Netflix")
};

// Health Check Root
app.MapGet("/", () => Results.Ok(new { status = "Online", service = "VaultFlow API" }))
   .WithTags("VaultFlow.Api");

// -------------------------------------------------------------
// ENDPOINTS DE TRANSACCIONES (VaultFlow Core API)
// -------------------------------------------------------------

app.MapGet("/api/transactions", () => Results.Ok(transactions))
   .WithName("GetTransactions")
   .WithTags("Transactions");

app.MapPost("/api/transactions", (TransactionInput input) =>
{
    var newTx = new Transaction(
        transactions.Count + 1,
        input.Description,
        input.Amount,
        string.IsNullOrEmpty(input.Category) ? "General" : input.Category,
        string.IsNullOrEmpty(input.Merchant) ? "Desconocido" : input.Merchant
    );
    transactions.Add(newTx);
    return Results.Created($"/api/transactions/{newTx.Id}", newTx);
})
.WithName("CreateTransaction")
.WithTags("Transactions");

// -------------------------------------------------------------
// ENDPOINT DE AGENTE IA (CON CONTEXTO DE TRANSACCIONES)
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

        // Formateo del contexto enviado por el frontend
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

// DTOs / Modelos
public record Transaction(int Id, string Description, decimal Amount, string Category, string Merchant);
public record TransactionInput(string Description, decimal Amount, string Category, string Merchant);
public record ChatTransaction(string Description, decimal Amount, string Category, string Merchant);
public record ChatRequest(string Prompt, List<ChatTransaction>? Transactions);