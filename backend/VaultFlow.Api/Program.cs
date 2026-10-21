using System.Text.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.EntityFrameworkCore;
using VaultFlow.Api.Data;
using VaultFlow.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Configuración de servicios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cadena de conexión (Azure SQL) desde configuración o variables de entorno.
string GetConnectionString(IConfiguration config) =>
    config.GetConnectionString("DefaultConnection")
    ?? config["SqlConnectionString"]
    ?? string.Empty;

var connectionString = GetConnectionString(builder.Configuration);
var keyVaultUri = builder.Configuration["KeyVaultUri"];

// En Azure, los secretos se leen desde Key Vault con la identidad administrada.
// Container Apps Express no soporta keyVaultUrl en los secretos, por eso se resuelven al iniciar.
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    var secretClient = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());

    if (string.IsNullOrWhiteSpace(connectionString) ||
        connectionString.Contains("YOUR_AZURE_SQL_CONNECTION_STRING_HERE", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            connectionString = secretClient.GetSecret("sql-connection-string").Value.Value;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No se pudo leer el secreto 'sql-connection-string' desde Key Vault. " +
                "Verifica la identidad administrada y el rol Key Vault Secrets User.", ex);
        }
    }

    if (string.IsNullOrWhiteSpace(builder.Configuration["GroqApiKey"]))
    {
        try
        {
            builder.Configuration["GroqApiKey"] = secretClient.GetSecret("groq-api-key").Value.Value;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"No se pudo leer 'groq-api-key' desde Key Vault: {ex.Message}");
        }
    }
}

if (string.IsNullOrWhiteSpace(connectionString) ||
    connectionString.Contains("YOUR_AZURE_SQL_CONNECTION_STRING_HERE", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "La cadena de conexión 'DefaultConnection' no está configurada. " +
        "Define ConnectionStrings__DefaultConnection, configura KeyVaultUri o usa appsettings.Development.json.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Cliente HTTP para Groq
builder.Services.AddHttpClient("GroqClient", client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
});

// Configuración de CORS permisivo
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");

// Health Check Root
app.MapGet("/", () => Results.Ok(new { status = "Online", service = "VaultFlow API" }))
   .WithTags("VaultFlow.Api");

// -------------------------------------------------------------
// ENDPOINTS DE TRANSACCIONES (Azure SQL + EF Core)
// -------------------------------------------------------------

// GET /api/transactions - Consulta datos desde Azure SQL
app.MapGet("/api/transactions", async (AppDbContext db) =>
{
    var transactions = await db.Transactions
        .AsNoTracking()
        .OrderByDescending(t => t.Id)
        .ToListAsync();

    return Results.Ok(transactions);
})
.WithName("GetTransactions")
.WithTags("Transactions");

// POST /api/transactions - Inserta un nuevo registro en Azure SQL
app.MapPost("/api/transactions", async (AppDbContext db, TransactionInput input) =>
{
    if (string.IsNullOrWhiteSpace(input.Description))
    {
        return Results.BadRequest(new { error = "La descripción es obligatoria." });
    }

    var transaction = new Transaction
    {
        Description = input.Description.Trim(),
        Amount = input.Amount,
        Category = string.IsNullOrWhiteSpace(input.Category) ? "General" : input.Category.Trim(),
        Merchant = string.IsNullOrWhiteSpace(input.Merchant) ? "Desconocido" : input.Merchant.Trim(),
        Date = DateTime.UtcNow
    };

    db.Transactions.Add(transaction);
    await db.SaveChangesAsync();

    return Results.Created($"/api/transactions/{transaction.Id}", transaction);
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
public record TransactionInput(string Description, decimal Amount, string Category, string Merchant);
public record ChatTransaction(string Description, decimal Amount, string Category, string Merchant);
public record ChatRequest(string Prompt, List<ChatTransaction>? Transactions);
