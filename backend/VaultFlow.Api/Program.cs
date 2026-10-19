using Microsoft.EntityFrameworkCore;
using OpenAI;
using System.ClientModel;
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
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["GroqApiKey"] 
        ?? throw new InvalidOperationException("La clave 'GroqApiKey' no está configurada.");

    return new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }
    );
});

/* Conexión a Azure SQL */
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

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

app.MapPost("/api/ai/chat", async (OpenAIClient openAiClient, ChatRequest request) =>
{
    var chatClient = openAiClient.GetChatClient("llama-3.3-70b-versatile");

    var completion = await chatClient.CompleteChatAsync(request.Prompt);
    return Results.Ok(new { response = completion.Value.Content[0].Text });
})
.WithName("AskAIAgent")
.WithTags("AI Agent");

app.Run();

public record ChatRequest(string Prompt);