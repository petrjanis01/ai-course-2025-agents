# 05 - Qdrant a Embeddings

## Cíl
Nastavit Qdrant vektorovou databázi a implementovat embedding service pomocí all-MiniLM-L6-v2 modelu (384 dimenzí).

## Prerekvizity
- Dokončený krok 08 (seed data)
- Ollama nainstalováno a běžící
- Qdrant běžící v Dockeru

## Kroky implementace

### 1. Spuštění Qdrant

```bash
docker run -d \
  --name qdrant-transactions \
  -p 6333:6333 \
  -p 6334:6334 \
  qdrant/qdrant:latest
```

Ověř, že Qdrant běží:
```bash
curl http://localhost:6333/health
# Očekávaná odpověď: {"title":"qdrant - vector search engine","version":"..."}
```

### 2. Stažení embedding modelu v Ollama

```bash
# Ujisti se, že Ollama běží
ollama serve

# Stáhni embedding model
ollama pull all-minilm:l6-v2
```

Ověř model:
```bash
ollama list
# Měl bys vidět: all-minilm:l6-v2
```

### 3. Embedding Service

**Services/EmbeddingService.cs:**
```csharp
using System.Text;
using System.Text.Json;

namespace TransactionManagement.Api.Services;

public interface IEmbeddingService
{
    Task<float[]> CreateEmbeddingAsync(string text);
    Task<List<float[]>> CreateEmbeddingsAsync(List<string> texts);
}

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _model = configuration["Embedding:Model"] ?? "all-minilm:l6-v2";
        _logger = logger;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty", nameof(text));
        }

        var request = new
        {
            model = _model,
            prompt = text
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/api/embeddings", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson);

            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                throw new Exception("No embedding returned from Ollama");
            }

            _logger.LogDebug("Created embedding: {Dimensions} dimensions", result.Embedding.Length);
            return result.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating embedding for text: {Text}", text.Substring(0, Math.Min(100, text.Length)));
            throw;
        }
    }

    public async Task<List<float[]>> CreateEmbeddingsAsync(List<string> texts)
    {
        var embeddings = new List<float[]>();

        foreach (var text in texts)
        {
            var embedding = await CreateEmbeddingAsync(text);
            embeddings.Add(embedding);
        }

        return embeddings;
    }

    private class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
```

### 4. Qdrant Service

**Services/QdrantService.cs:**
```csharp
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TransactionManagement.Api.Configuration;
using Microsoft.Extensions.Options;

namespace TransactionManagement.Api.Services;

public interface IQdrantService
{
    Task InitializeCollectionAsync();
    Task<bool> CollectionExistsAsync();
    Task IndexDocumentChunkAsync(DocumentChunk chunk);
    Task<List<SearchResult>> SearchAsync(string query, SearchFilters? filters = null, int limit = 10);
    Task DeleteDocumentAsync(int attachmentId);
}

public class QdrantService : IQdrantService
{
    private readonly QdrantClient _client;
    private readonly IEmbeddingService _embeddingService;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(
        IOptions<QdrantOptions> options,
        IEmbeddingService embeddingService,
        ILogger<QdrantService> logger)
    {
        _options = options.Value;
        _embeddingService = embeddingService;
        _logger = logger;

        _client = new QdrantClient(_options.Url);
    }

    public async Task<bool> CollectionExistsAsync()
    {
        try
        {
            var collections = await _client.ListCollectionsAsync();
            return collections.Any(c => c.Name == _options.CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if collection exists");
            return false;
        }
    }

    public async Task InitializeCollectionAsync()
    {
        if (await CollectionExistsAsync())
        {
            _logger.LogInformation("Collection {CollectionName} already exists", _options.CollectionName);
            return;
        }

        _logger.LogInformation("Creating collection {CollectionName} with {VectorSize} dimensions",
            _options.CollectionName, _options.VectorSize);

        await _client.CreateCollectionAsync(
            collectionName: _options.CollectionName,
            vectorsConfig: new VectorParams
            {
                Size = (ulong)_options.VectorSize,
                Distance = Distance.Cosine
            });

        _logger.LogInformation("Collection {CollectionName} created successfully", _options.CollectionName);
    }

    public async Task IndexDocumentChunkAsync(DocumentChunk chunk)
    {
        // Create embedding
        var embedding = await _embeddingService.CreateEmbeddingAsync(chunk.Content);

        // Create point ID
        var pointId = $"{chunk.AttachmentId}-chunk-{chunk.ChunkIndex}";

        // Prepare payload
        var payload = new Dictionary<string, Value>
        {
            ["attachment_id"] = chunk.AttachmentId,
            ["transaction_id"] = chunk.TransactionId,
            ["chunk_index"] = chunk.ChunkIndex,
            ["total_chunks"] = chunk.TotalChunks,
            ["category"] = chunk.Category ?? "",
            ["file_name"] = chunk.FileName,
            ["content"] = chunk.Content,
            ["token_count"] = chunk.TokenCount,
            ["has_amounts"] = chunk.HasAmounts,
            ["has_dates"] = chunk.HasDates,
            ["word_count"] = chunk.WordCount,
            ["created_at"] = DateTime.UtcNow.ToString("O")
        };

        // Upsert to Qdrant
        var point = new PointStruct
        {
            Id = new PointId { Uuid = pointId },
            Vectors = embedding,
            Payload = { payload }
        };

        await _client.UpsertAsync(
            collectionName: _options.CollectionName,
            points: new[] { point });

        _logger.LogDebug("Indexed chunk {ChunkIndex}/{TotalChunks} for attachment {AttachmentId}",
            chunk.ChunkIndex, chunk.TotalChunks, chunk.AttachmentId);
    }

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        SearchFilters? filters = null,
        int limit = 10)
    {
        // Create query embedding
        var queryEmbedding = await _embeddingService.CreateEmbeddingAsync(query);

        // Build filter conditions
        var conditions = new List<Condition>();

        if (filters != null)
        {
            if (!string.IsNullOrEmpty(filters.Category))
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "category",
                        Match = new Match { Keyword = filters.Category }
                    }
                });
            }

            if (filters.TransactionId.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "transaction_id",
                        Match = new Match { Integer = filters.TransactionId.Value }
                    }
                });
            }

            if (filters.HasAmounts.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "has_amounts",
                        Match = new Match { Boolean = filters.HasAmounts.Value }
                    }
                });
            }

            if (filters.HasDates.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "has_dates",
                        Match = new Match { Boolean = filters.HasDates.Value }
                    }
                });
            }
        }

        Filter? filter = conditions.Any()
            ? new Filter { Must = { conditions } }
            : null;

        // Search
        var searchResults = await _client.SearchAsync(
            collectionName: _options.CollectionName,
            vector: queryEmbedding,
            filter: filter,
            limit: (ulong)limit,
            scoreThreshold: 0.5f);

        // Map results
        return searchResults.Select(r => new SearchResult
        {
            AttachmentId = (int)r.Payload["attachment_id"].IntegerValue,
            TransactionId = (int)r.Payload["transaction_id"].IntegerValue,
            ChunkIndex = (int)r.Payload["chunk_index"].IntegerValue,
            TotalChunks = (int)r.Payload["total_chunks"].IntegerValue,
            Category = r.Payload["category"].StringValue,
            FileName = r.Payload["file_name"].StringValue,
            Content = r.Payload["content"].StringValue,
            Score = r.Score,
            HasAmounts = r.Payload["has_amounts"].BoolValue,
            HasDates = r.Payload["has_dates"].BoolValue,
            WordCount = (int)r.Payload["word_count"].IntegerValue
        }).ToList();
    }

    public async Task DeleteDocumentAsync(int attachmentId)
    {
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "attachment_id",
                        Match = new Match { Integer = attachmentId }
                    }
                }
            }
        };

        await _client.DeleteAsync(
            collectionName: _options.CollectionName,
            filter: filter);

        _logger.LogInformation("Deleted all chunks for attachment {AttachmentId}", attachmentId);
    }
}

// Supporting models
public class DocumentChunk
{
    public int AttachmentId { get; set; }
    public int TransactionId { get; set; }
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string? Category { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public bool HasAmounts { get; set; }
    public bool HasDates { get; set; }
    public int WordCount { get; set; }
}

public class SearchResult
{
    public int AttachmentId { get; set; }
    public int TransactionId { get; set; }
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float Score { get; set; }
    public bool HasAmounts { get; set; }
    public bool HasDates { get; set; }
    public int WordCount { get; set; }
}

public class SearchFilters
{
    public string? Category { get; set; }
    public int? TransactionId { get; set; }
    public bool? HasAmounts { get; set; }
    public bool? HasDates { get; set; }
}
```

### 5. Registrace v Program.cs

```csharp
// Register HttpClient for Ollama
builder.Services.AddHttpClient("Ollama", client =>
{
    var baseUrl = builder.Configuration["LLM:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Register services
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();

// Initialize Qdrant collection on startup
using (var scope = app.Services.CreateScope())
{
    var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();
    await qdrantService.InitializeCollectionAsync();
}
```

## Testování

### 1. Test embedding service

Vytvoř test endpoint:

**Controllers/TestController.cs:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpPost("embedding")]
    public async Task<IActionResult> TestEmbedding(
        [FromBody] string text,
        [FromServices] IEmbeddingService embeddingService)
    {
        var embedding = await embeddingService.CreateEmbeddingAsync(text);

        return Ok(new
        {
            text,
            dimensions = embedding.Length,
            embedding = embedding.Take(10).ToArray(), // First 10 values
            sample = $"[{string.Join(", ", embedding.Take(5).Select(x => x.ToString("F4")))}...]"
        });
    }

    [HttpPost("search")]
    public async Task<IActionResult> TestSearch(
        [FromBody] string query,
        [FromServices] IQdrantService qdrantService)
    {
        var results = await qdrantService.SearchAsync(query, limit: 5);

        return Ok(new
        {
            query,
            resultsCount = results.Count,
            results = results.Select(r => new
            {
                r.AttachmentId,
                r.FileName,
                r.Score,
                contentPreview = r.Content.Substring(0, Math.Min(100, r.Content.Length))
            })
        });
    }
}
```

### 2. Test curl

```bash
# Test embedding
curl -X POST http://localhost:5000/api/test/embedding \
  -H "Content-Type: application/json" \
  -d '"Toto je testovací text pro embedding"'

# Očekávaný výsledek:
# {
#   "text": "Toto je testovací text pro embedding",
#   "dimensions": 384,
#   "embedding": [...],
#   "sample": "[0.1234, -0.5678, ...]"
# }
```

### 3. Ověření Qdrant

```bash
# Zobraz kolekce
curl http://localhost:6333/collections

# Detail kolekce
curl http://localhost:6333/collections/transaction_documents

# Očekávaný výsledek:
# {
#   "result": {
#     "status": "green",
#     "vectors_count": 0,
#     "points_count": 0,
#     "config": {
#       "params": {
#         "vectors": {
#           "size": 384,
#           "distance": "Cosine"
#         }
#       }
#     }
#   }
# }
```

### 4. Qdrant Web UI

Otevři prohlížeč:
```
http://localhost:6333/dashboard
```

- Ověř, že kolekce `transaction_documents` existuje
- Zkontroluj konfiguraci (384 dimenzí, Cosine distance)

## Ověření

Po dokončení:

1. ✅ Qdrant běží na portu 6333
2. ✅ Ollama běží a má model all-minilm:l6-v2
3. ✅ EmbeddingService vytváří 384-dimenzní vektory
4. ✅ Kolekce `transaction_documents` existuje v Qdrantu
5. ✅ QdrantService umí indexovat a vyhledávat

## Docker Compose v2 - Přidání Qdrant a Ollama

Aktualizuj **docker-compose.yml** o Qdrant a Ollama služby:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    container_name: postgres-transactions
    environment:
      POSTGRES_DB: transactionsdb
      POSTGRES_USER: appuser
      POSTGRES_PASSWORD: AppPassword123
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U appuser -d transactionsdb"]
      interval: 10s
      timeout: 5s
      retries: 5

  qdrant:
    image: qdrant/qdrant:latest
    container_name: qdrant-transactions
    ports:
      - "6333:6333"  # HTTP API
      - "6334:6334"  # gRPC API
    volumes:
      - qdrant_data:/qdrant/storage
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 10s
      timeout: 5s
      retries: 5

  ollama:
    image: ollama/ollama:latest
    container_name: ollama-embeddings
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:11434/api/tags"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Init service to pull the embedding model
  ollama-init:
    image: ollama/ollama:latest
    container_name: ollama-init
    depends_on:
      ollama:
        condition: service_healthy
    volumes:
      - ollama_data:/root/.ollama
    entrypoint: ["/bin/sh", "-c"]
    command:
      - |
        echo "Pulling all-minilm embedding model..."
        ollama pull all-minilm:l6-v2
        echo "Model pulled successfully"
    restart: "no"

  backend:
    build:
      context: ./TransactionManagement.Api
      dockerfile: Dockerfile
    container_name: backend-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=transactionsdb;Username=appuser;Password=AppPassword123
      - Qdrant__Url=http://qdrant:6333
      - Qdrant__CollectionName=transaction_documents
      - Qdrant__VectorSize=384
      - LLM__BaseUrl=http://ollama:11434
      - LLM__Model=llama3.1:8b
      - Embedding__Model=all-minilm:l6-v2
    ports:
      - "5000:5000"
    depends_on:
      postgres:
        condition: service_healthy
      qdrant:
        condition: service_healthy
      ollama:
        condition: service_healthy
    volumes:
      - ./TransactionManagement.Api/uploads:/app/uploads

  frontend:
    build:
      context: ./transaction-management-ui
      dockerfile: Dockerfile
    container_name: frontend-ui
    ports:
      - "3000:80"
    depends_on:
      - backend
    environment:
      - REACT_APP_API_URL=http://localhost:5000/api

volumes:
  postgres_data:
  qdrant_data:
  ollama_data:
```

### Aktualizace appsettings.json

**TransactionManagement.Api/appsettings.json:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transactionsdb;Username=appuser;Password=AppPassword123"
  },
  "Qdrant": {
    "Url": "http://localhost:6333",
    "CollectionName": "transaction_documents",
    "VectorSize": 384
  },
  "LLM": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.1:8b"
  },
  "Embedding": {
    "Model": "all-minilm:l6-v2"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Configuration Options class

**Configuration/QdrantOptions.cs:**
```csharp
namespace TransactionManagement.Api.Configuration;

public class QdrantOptions
{
    public string Url { get; set; } = "http://localhost:6333";
    public string CollectionName { get; set; } = "transaction_documents";
    public int VectorSize { get; set; } = 384;
}
```

**Program.cs** - registrace options:
```csharp
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
```

### Spuštění aktualizovaného stacku

```bash
# Zastavení starého stacku
docker-compose down

# Build a spuštění s novými službami
docker-compose up --build

# Sledování logů
docker-compose logs -f ollama qdrant

# Ověření, že Ollama stáhlo model
docker-compose logs ollama-init
```

### Přístup k novým službám

- **Qdrant Dashboard**: http://localhost:6333/dashboard
- **Qdrant API**: http://localhost:6333
- **Ollama API**: http://localhost:11434

### Ověření setupu

```bash
# Zkontroluj Qdrant health
curl http://localhost:6333/health

# Zkontroluj Qdrant kolekce
curl http://localhost:6333/collections

# Zkontroluj Ollama modely
curl http://localhost:11434/api/tags

# Test embedding
curl -X POST http://localhost:5000/api/test/embedding \
  -H "Content-Type: application/json" \
  -d '"Test text for embedding"'
```

### Volume management

```bash
# Zobraz volumes
docker volume ls

# Smaž Qdrant data (fresh start)
docker-compose down
docker volume rm ai-course-2025_qdrant_data

# Smaž Ollama data (znovu stáhne model)
docker volume rm ai-course-2025_ollama_data
```

### Troubleshooting

**Ollama model se nestáhl:**
```bash
# Manuální pull modelu
docker-compose exec ollama ollama pull all-minilm:l6-v2
docker-compose restart backend
```

**Qdrant není dostupný:**
```bash
# Zkontroluj logy
docker-compose logs qdrant

# Restart Qdrant
docker-compose restart qdrant
```

**Backend nemůže spojit s Qdrant/Ollama:**
```bash
# Zkontroluj network
docker-compose ps
docker network inspect ai-course-2025_default

# Ujisti se, že backend startuje až po health check
docker-compose logs backend
```

## Výstup této fáze

✅ Qdrant vektorová databáze připravená
✅ EmbeddingService s all-MiniLM-L6-v2 (384 dimenzí)
✅ QdrantService pro indexování a vyhledávání
✅ Kolekce s Cosine similarity
✅ Support pro metadata filtering
✅ **Docker Compose s Qdrant a Ollama službami**
✅ **Automatické stažení embedding modelu**
✅ **Health checks pro všechny služby**

## Další krok

→ **10_document_processing.md** - Chunking, metadata enrichment a DocumentProcessingAgent
