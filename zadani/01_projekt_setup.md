# 01 - Založení .NET 8 Projektu

## Cíl
Vytvořit základní strukturu .NET 8 Web API projektu s potřebnými dependencies.

## Prerekvizity
- .NET 8 SDK nainstalováno
- Visual Studio 2022 / VS Code / Rider
- Docker Desktop (pro pozdější fáze)

## Kroky implementace

### 1. Vytvoření projektu

```bash
# Vytvoř složku pro projekt
mkdir transaction-management
cd transaction-management

# Inicializuj git repository
git init

# Vytvoř .gitignore pro .NET a React (viz níže)

# Vytvoř .NET 8 Web API projekt
dotnet new webapi -n TransactionManagement.Api
cd TransactionManagement.Api

# Vytvoř solution
cd ..
dotnet new sln -n TransactionManagement
dotnet sln add TransactionManagement.Api/TransactionManagement.Api.csproj
```

### 1a. Vytvoření .gitignore

Vytvoř **.gitignore** v root složce projektu (transaction-management/):

```gitignore
# .NET
*.swp
*.*~
project.lock.json
.DS_Store
*.pyc
nupkg/

# Visual Studio Code
.vscode/

# Rider
.idea/

# Visual Studio
.vs/
bin/
obj/
out/
*.user
*.suo
*.userosscache
*.sln.docstates
*.userprefs

# Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
[Aa]rm/
[Aa]rm64/
bld/
[Bb]in/
[Oo]bj/
[Ll]og/
[Ll]ogs/

# MSTest test Results
[Tt]est[Rr]esult*/
[Bb]uild[Ll]og.*

# .NET Core
project.lock.json
project.fragment.lock.json
artifacts/

# ASP.NET Scaffolding
ScaffoldingReadMe.txt

# NuGet Packages
*.nupkg
*.snupkg
**/packages/*
!**/packages/build/
*.nuget.props
*.nuget.targets

# Files uploaded by users
uploads/
attachments/
data/

# Environment variables
.env
.env.local
.env.*.local

# React / Node.js
node_modules/
npm-debug.log*
yarn-debug.log*
yarn-error.log*
lerna-debug.log*
.pnpm-debug.log*

# React build output
build/
dist/
*.local

# Testing
coverage/
*.lcov
.nyc_output

# React misc
.DS_Store
.env.local
.env.development.local
.env.test.local
.env.production.local

# Logs
logs
*.log

# Temporary files
*.tmp
*.temp
.cache/

# Docker
docker-compose.override.yml

# Database
*.db
*.db-shm
*.db-wal
*.sqlite
*.sqlite3

# Secrets (Langfuse API keys, etc.)
secrets/
*.key
*.pem

# OS
Thumbs.db
ehthumbs.db
Desktop.ini
$RECYCLE.BIN/
.AppleDouble
.LSOverride
._*

# JetBrains Rider
.idea/
*.sln.iml
```

**Poznámky k .gitignore:**
- ✅ Ignoruje build artifacts (.NET bin/, obj/)
- ✅ Ignoruje node_modules (React)
- ✅ Ignoruje uživatelská data (uploads/, attachments/)
- ✅ Ignoruje .env soubory se secrets
- ✅ Ignoruje IDE konfigurace (.vscode/, .idea/, .vs/)
- ✅ Ignoruje databázové soubory
- ✅ Ignoruje Docker override soubory

### 2. Instalace NuGet balíčků

```bash
cd TransactionManagement.Api

# Entity Framework Core + PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0

# Swagger/OpenAPI
dotnet add package Swashbuckle.AspNetCore --version 6.5.0

# Microsoft Agent Framework (Semantic Kernel)
dotnet add package Microsoft.SemanticKernel --version 1.0.1

# Qdrant Client
dotnet add package Qdrant.Client --version 1.8.0

# Langfuse SDK
dotnet add package Langfuse --version 1.0.0

# Další utility
dotnet add package Serilog.AspNetCore --version 8.0.0
dotnet add package Serilog.Sinks.Console --version 5.0.1
```

### 3. Struktura projektu

Vytvoř následující strukturu složek:

```
TransactionManagement.Api/
├── Controllers/
│   ├── TransactionsController.cs
│   ├── AttachmentsController.cs
│   ├── ChatController.cs
│   └── DocumentsController.cs
├── Models/
│   ├── Entities/
│   │   ├── Transaction.cs
│   │   ├── Attachment.cs
│   │   ├── ChatSession.cs
│   │   └── ChatMessage.cs
│   └── DTOs/
│       ├── TransactionDto.cs
│       ├── AttachmentDto.cs
│       └── ChatMessageDto.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Migrations/
├── Services/
│   ├── Agents/
│   │   ├── DocumentProcessingAgent.cs
│   │   ├── ChatOrchestrator.cs
│   │   ├── DatabaseAgent.cs
│   │   └── DocumentSearchAgent.cs
│   ├── EmbeddingService.cs
│   ├── QdrantService.cs
│   ├── LangfuseService.cs
│   ├── ChunkingService.cs
│   ├── MetadataEnrichmentService.cs
│   ├── FileStorageService.cs
│   └── BackgroundTaskQueue.cs
├── Configuration/
│   ├── QdrantOptions.cs
│   ├── LangfuseOptions.cs
│   └── LLMOptions.cs
├── appsettings.json
├── appsettings.Development.json
└── Program.cs
```

### 4. Konfigurace appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transactionsdb;Username=appuser;Password=apppass123"
  },
  "Qdrant": {
    "Url": "http://localhost:6333",
    "CollectionName": "transaction_documents",
    "VectorSize": 384
  },
  "Langfuse": {
    "BaseUrl": "http://localhost:3030",
    "PublicKey": "",
    "SecretKey": ""
  },
  "LLM": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.1:8b"
  },
  "Embedding": {
    "Model": "all-minilm:l6-v2"
  },
  "FileStorage": {
    "BasePath": "/app/data/attachments"
  }
}
```

### 5. Vytvoření configuration classes

**Configuration/QdrantOptions.cs:**
```csharp
namespace TransactionManagement.Api.Configuration;

public class QdrantOptions
{
    public string Url { get; set; } = string.Empty;
    public string CollectionName { get; set; } = "transaction_documents";
    public int VectorSize { get; set; } = 384;
}
```

**Configuration/LangfuseOptions.cs:**
```csharp
namespace TransactionManagement.Api.Configuration;

public class LangfuseOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}
```

**Configuration/LLMOptions.cs:**
```csharp
namespace TransactionManagement.Api.Configuration;

public class LLMOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
```

### 6. Základní Program.cs setup

```csharp
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Configuration;
using TransactionManagement.Api.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure options
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<LangfuseOptions>(
    builder.Configuration.GetSection("Langfuse"));
builder.Services.Configure<LLMOptions>(
    builder.Configuration.GetSection("LLM"));

// Configure DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

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
```

## Ověření

Po dokončení:

1. **Build projektu:**
```bash
dotnet build
```

2. **Spuštění projektu:**
```bash
dotnet run
```

3. **Ověření Swagger UI:**
   - Otevři prohlížeč: `http://localhost:5000/swagger`
   - Měl by se zobrazit Swagger UI (zatím bez endpointů)

## Výstup této fáze

✅ Funkční .NET 8 Web API projekt
✅ **Git repository s kompletním .gitignore**
✅ **.gitignore pokrývá .NET, React, Docker, Secrets**
✅ Všechny potřebné NuGet balíčky nainstalované
✅ Základní struktura složek
✅ Konfigurace v appsettings.json
✅ Program.cs s DI setupem
✅ Projekt se builne a spouští

## Další krok

→ **02_databaze_a_modely.md** - Vytvoření databázových entit a EF Core konfigurace
