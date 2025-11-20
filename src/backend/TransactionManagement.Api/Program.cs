using TransactionManagement.Api.Configuration;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Services;
using TransactionManagement.Api.Services.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Configure OpenTelemetry for Langfuse
var langfuseSection = builder.Configuration.GetSection("Langfuse");
var langfuseBaseUrl = langfuseSection.GetValue<string>("BaseUrl") ?? "http://localhost:3030";
var langfusePublicKey = langfuseSection.GetValue<string>("PublicKey") ?? "";
var langfuseSecretKey = langfuseSection.GetValue<string>("SecretKey") ?? "";

// Only configure OpenTelemetry if Langfuse keys are provided
if (!string.IsNullOrEmpty(langfusePublicKey) && !string.IsNullOrEmpty(langfuseSecretKey))
{
    var authString = Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{langfusePublicKey}:{langfuseSecretKey}"));

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: "TransactionManagement", serviceVersion: "1.0.0"))
        .WithTracing(tracerBuilder =>
        {
            tracerBuilder
                // Must match sourceName used in UseOpenTelemetry
                .AddSource("TransactionManagement")
                .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri($"{langfuseBaseUrl}/api/public/otel/v1/traces");
                    options.Protocol = OtlpExportProtocol.HttpProtobuf; // Langfuse requires HTTP/protobuf
                    options.Headers = $"Authorization=Basic {authString}";
                });
        });

    Log.Information("OpenTelemetry configured for Langfuse at {BaseUrl}", langfuseBaseUrl);
}
else
{
    Log.Warning("Langfuse keys not configured, OpenTelemetry export disabled");
}

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings instead of numbers
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HttpClient for Ollama
builder.Services.AddHttpClient("Ollama", client =>
{
    var baseUrl = builder.Configuration["LLM:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Register services
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISeedDataService, SeedDataService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();

// Document processing services
builder.Services.AddScoped<IChunkingService, ChunkingService>();
builder.Services.AddScoped<IMetadataEnrichmentService, MetadataEnrichmentService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<ILLMService, LLMService>();

// Background task queue
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();

// === Agent Framework Services ===

// Chat Client Service pro Ollama
builder.Services.AddSingleton<IChatClientService, ChatClientService>();

// Function services (scoped - kvůli DbContext)
builder.Services.AddScoped<DatabaseFunctions>();
builder.Services.AddScoped<DocumentFunctions>();

// Chat Orchestrator
builder.Services.AddScoped<IChatOrchestrator, ChatOrchestrator>();

// Configure multipart form data for file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});

// Configure options
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<LangfuseOptions>(
    builder.Configuration.GetSection("Langfuse"));
builder.Services.Configure<LLMOptions>(
    builder.Configuration.GetSection("LLM"));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Run database migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    Log.Information("Running database migrations...");
    db.Database.Migrate();
    Log.Information("Database migrations completed successfully.");
}

// Initialize Qdrant collection on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();
        await qdrantService.InitializeCollectionAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to initialize Qdrant collection. Qdrant may not be available.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
