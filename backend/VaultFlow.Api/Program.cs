using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using VaultFlow.Api.Data;

var builder = WebApplication.CreateBuilder(args);
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
    var endpoint = config["AIServicesEndpoint"] ?? config["AIServices:Endpoint"]
                ?? throw new InvalidOperationException("El endpoint de AI Services no está configurado.");
    return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
});

// Conexión a Azure SQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

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

app.MapPost("/api/ai/chat", async (AzureOpenAIClient aiClient, ChatRequest request) =>
{
    var chatClient = aiClient.GetChatClient("gpt-4o-mini");

    var completion = await chatClient.CompleteChatAsync(request.Prompt);
    return Results.Ok(new { response = completion.Value.Content[0].Text });
})
.WithName("AskAIAgent")
.WithTags("AI Agent");

app.Run();
public record ChatRequest(string Prompt);