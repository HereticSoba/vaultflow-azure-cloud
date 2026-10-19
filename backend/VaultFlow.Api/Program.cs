using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using VaultFlow.Api.Data;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cliente HTTP dedicado para Groq API
builder.Services.AddHttpClient("GroqClient", client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
});

// Conexión a Azure SQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no me encuentra.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

app.UseCors();

// Swagger en Desarrollo y Producción
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VaultFlow API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthorization();
app.MapControllers();

// Endpoint del agente de IA consumiendo Groq por HTTP directo
app.MapPost("/api/ai/chat", async (IHttpClientFactory httpClientFactory, IConfiguration config, ChatRequest request) =>
{
    var apiKey = config["GroqApiKey"];
    if (string.IsNullOrEmpty(apiKey))
    {
        return Results.Problem("La clave GroqApiKey no está configurada en las variables de entorno.", statusCode: 500);
    }

    var client = httpClientFactory.CreateClient("GroqClient");
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

    var payload = new
    {
        model = "llama-3.3-70b-versatile",
        messages = new[]
        {
            new { role = "system", content = "Eres un asistente virtual de IA integrado en la plataforma VaultFlow." },
            new { role = "user", content = request.Prompt }
        }
    };

    var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    var response = await client.PostAsync("chat/completions", jsonContent);

    if (!response.IsSuccessStatusCode)
    {
        var errorBody = await response.Content.ReadAsStringAsync();
        return Results.Problem($"Error en la API de Groq: {errorBody}", statusCode: (int)response.StatusCode);
    }

    var responseStream = await response.Content.ReadAsStreamAsync();
    using var jsonDoc = await JsonDocument.ParseAsync(responseStream);
    var replyText = jsonDoc.RootElement
        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString();

    return Results.Ok(new { response = replyText });
})
.WithName("AskAIAgent")
.WithTags("AI Agent");

app.Run();

public record ChatRequest(string Prompt);