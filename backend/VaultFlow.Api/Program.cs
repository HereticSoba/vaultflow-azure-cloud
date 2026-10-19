using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VaultFlow.Api.Data;

var builder = WebApplication.CreateBuilder(args);

/* CORS */
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

/* Cliente HTTP para Groq */
builder.Services.AddHttpClient("GroqClient", client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
});

/* Configuración db */
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
}

var app = builder.Build();

app.UseCors();

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

/* Endpoint Agente IA */
app.MapPost("/api/ai/chat", async (IHttpClientFactory httpClientFactory, IConfiguration config, ChatRequest request) =>
{
    try
    {
        var apiKey = config["GroqApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return Results.Json(new { error = "La variable 'GroqApiKey' no está configurada o se encuentra vacía." }, statusCode: 500);
        }

        var client = httpClientFactory.CreateClient("GroqClient");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model = "llama-3.3-70b-versatile", // <--- Asegura este ID de modelo exacto de Groq
            messages = new[]
            {
                new { role = "system", content = "Eres un asistente virtual de IA integrado en la plataforma VaultFlow." },
                new { role = "user", content = request.Prompt }
            }
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("chat/completions", jsonContent);

        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new { error = $"Groq API error ({response.StatusCode}): {responseString}" }, statusCode: (int)response.StatusCode);
        }

        using var jsonDoc = JsonDocument.Parse(responseString);
        var replyText = jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return Results.Ok(new { response = replyText });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = $"Excepción en el servidor: {ex.Message}", details = ex.ToString() }, statusCode: 500);
    }
})
.WithName("AskAIAgent")
.WithTags("AI Agent");

app.Run();
public record ChatRequest(string Prompt);