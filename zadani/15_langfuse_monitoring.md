# 08 - Langfuse Monitoring

## Cíl
Integrovat Langfuse pro monitoring a tracing všech LLM interakcí (categorization, embeddings, chat).

## Prerekvizity
- Dokončený krok 13 (chat agenti)
- Langfuse běžící v Dockeru

## Kroky implementace

### 1. Spuštění Langfuse pro vývoj

Pro lokální vývoj můžeš použít separátní Langfuse instanci:

```bash
# Vytvoř databázi v existujícím PostgreSQL
docker exec -it postgres-dev psql -U appuser -c "CREATE DATABASE langfusedb;"

# Spusť Langfuse
docker run -d \
  --name langfuse-dev \
  -p 3030:3000 \
  -e DATABASE_URL=postgresql://appuser:apppass123@host.docker.internal:5432/langfusedb \
  -e NEXTAUTH_URL=http://localhost:3030 \
  -e NEXTAUTH_SECRET=mysecretkey123456789 \
  -e SALT=mysaltkey123456789 \
  langfuse/langfuse:latest
```

**Poznámka:** V produkčním Docker Compose setupu (krok 11) bude vše nakonfigurováno automaticky s jedním sdíleným PostgreSQL serverem.

### 2. První přihlášení do Langfuse UI

1. Otevři prohlížeč: http://localhost:3030
2. Zaregistruj se (první uživatel je automaticky admin)
3. Vytvoř nový projekt (např. "Transaction Management")
4. Přejdi do Settings → API Keys
5. Vygeneruj nové API klíče:
   - Public Key (pk-lf-...)
   - Secret Key (sk-lf-...)

### 3. Aktualizace appsettings.json

```json
{
  "Langfuse": {
    "BaseUrl": "http://localhost:3030",
    "PublicKey": "pk-lf-your-public-key-here",
    "SecretKey": "sk-lf-your-secret-key-here"
  }
}
```

### 4. Langfuse Service

**Services/LangfuseService.cs:**
```csharp
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TransactionManagement.Api.Configuration;

namespace TransactionManagement.Api.Services;

public interface ILangfuseService
{
    Task<string> StartTraceAsync(string name, Dictionary<string, object>? metadata = null);
    Task LogGenerationAsync(string traceId, string name, string input, string output, Dictionary<string, object>? metadata = null);
    Task LogEmbeddingAsync(string traceId, string input, int dimensions);
    Task EndTraceAsync(string traceId);
}

public class LangfuseService : ILangfuseService
{
    private readonly HttpClient _httpClient;
    private readonly LangfuseOptions _options;
    private readonly ILogger<LangfuseService> _logger;

    public LangfuseService(
        IHttpClientFactory httpClientFactory,
        IOptions<LangfuseOptions> options,
        ILogger<LangfuseService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Langfuse");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> StartTraceAsync(string name, Dictionary<string, object>? metadata = null)
    {
        var traceId = Guid.NewGuid().ToString();

        var payload = new
        {
            id = traceId,
            name,
            timestamp = DateTime.UtcNow.ToString("O"),
            metadata = metadata ?? new Dictionary<string, object>()
        };

        try
        {
            await SendToLangfuseAsync("/api/public/traces", payload);
            _logger.LogDebug("Started trace: {TraceId} - {Name}", traceId, name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Langfuse trace");
        }

        return traceId;
    }

    public async Task LogGenerationAsync(
        string traceId,
        string name,
        string input,
        string output,
        Dictionary<string, object>? metadata = null)
    {
        var payload = new
        {
            id = Guid.NewGuid().ToString(),
            traceId,
            name,
            input,
            output,
            startTime = DateTime.UtcNow.ToString("O"),
            endTime = DateTime.UtcNow.ToString("O"),
            metadata = metadata ?? new Dictionary<string, object>(),
            model = "llama3.1:8b" // You can make this dynamic
        };

        try
        {
            await SendToLangfuseAsync("/api/public/generations", payload);
            _logger.LogDebug("Logged generation: {TraceId} - {Name}", traceId, name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log generation to Langfuse");
        }
    }

    public async Task LogEmbeddingAsync(string traceId, string input, int dimensions)
    {
        var metadata = new Dictionary<string, object>
        {
            ["dimensions"] = dimensions,
            ["model"] = "all-minilm:l6-v2",
            ["input_length"] = input.Length
        };

        await LogGenerationAsync(
            traceId,
            "embedding",
            input.Substring(0, Math.Min(200, input.Length)),
            $"[{dimensions}D vector]",
            metadata);
    }

    public async Task EndTraceAsync(string traceId)
    {
        // Langfuse automatically closes traces, but you can update if needed
        _logger.LogDebug("Ended trace: {TraceId}", traceId);
    }

    private async Task SendToLangfuseAsync(string endpoint, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Add auth headers
        var authString = $"{_options.PublicKey}:{_options.SecretKey}";
        var authBytes = Encoding.UTF8.GetBytes(authString);
        var authBase64 = Convert.ToBase64String(authBytes);
        request.Headers.Add("Authorization", $"Basic {authBase64}");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Langfuse request failed: {Status} - {Content}",
                response.StatusCode,
                errorContent);
        }
    }
}
```

### 5. Aktualizace EmbeddingService s Langfuse

**Services/EmbeddingService.cs** (aktualizuj metodu `CreateEmbeddingAsync`):

```csharp
public async Task<float[]> CreateEmbeddingAsync(string text, string? traceId = null)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        throw new ArgumentException("Text cannot be empty", nameof(text));
    }

    // Start Langfuse trace if not provided
    var shouldCreateTrace = string.IsNullOrEmpty(traceId);
    if (shouldCreateTrace)
    {
        traceId = await _langfuseService.StartTraceAsync("create_embedding");
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
        var startTime = DateTime.UtcNow;
        var response = await _httpClient.PostAsync("/api/embeddings", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson);

        if (result?.Embedding == null || result.Embedding.Length == 0)
        {
            throw new Exception("No embedding returned from Ollama");
        }

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Log to Langfuse
        await _langfuseService.LogEmbeddingAsync(traceId!, text, result.Embedding.Length);

        if (shouldCreateTrace)
        {
            await _langfuseService.EndTraceAsync(traceId!);
        }

        _logger.LogDebug("Created embedding: {Dimensions} dimensions in {Duration}ms",
            result.Embedding.Length, duration);

        return result.Embedding;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating embedding");
        throw;
    }
}
```

Přidej dependency do constructoru:
```csharp
private readonly ILangfuseService _langfuseService;

public EmbeddingService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILangfuseService langfuseService,
    ILogger<EmbeddingService> logger)
{
    _httpClient = httpClientFactory.CreateClient("Ollama");
    _model = configuration["Embedding:Model"] ?? "all-minilm:l6-v2";
    _langfuseService = langfuseService;
    _logger = logger;
}
```

### 6. Aktualizace LLMService s Langfuse

**Services/LLMService.cs** (aktualizuj metodu `CategorizeDocumentAsync`):

```csharp
public async Task<string> CategorizeDocumentAsync(string content, string? traceId = null)
{
    var shouldCreateTrace = string.IsNullOrEmpty(traceId);
    if (shouldCreateTrace)
    {
        traceId = await _langfuseService.StartTraceAsync("categorize_document");
    }

    var sampleContent = content.Length > 2000 ? content.Substring(0, 2000) : content;

    var systemPrompt = @"You are a document classifier...";
    var userPrompt = $"Classify this document:\n\n{sampleContent}";
    var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";

    var request = new
    {
        model = _model,
        prompt = fullPrompt,
        stream = false,
        options = new { temperature = 0.1, num_predict = 20 }
    };

    var json = JsonSerializer.Serialize(request);
    var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

    try
    {
        var startTime = DateTime.UtcNow;
        var response = await _httpClient.PostAsync("/api/generate", requestContent);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

        var category = result?.Response?.Trim().ToLower() ?? "unknown";
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Validate category
        var validCategories = new[] { "invoice", "contract", "purchase_order", "unknown" };
        if (!validCategories.Contains(category))
        {
            _logger.LogWarning("Invalid category: {Category}, defaulting to unknown", category);
            category = "unknown";
        }

        // Log to Langfuse
        var metadata = new Dictionary<string, object>
        {
            ["duration_ms"] = duration,
            ["input_length"] = content.Length,
            ["category"] = category
        };

        await _langfuseService.LogGenerationAsync(
            traceId!,
            "categorize_document",
            sampleContent,
            category,
            metadata);

        if (shouldCreateTrace)
        {
            await _langfuseService.EndTraceAsync(traceId!);
        }

        return category;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error categorizing document");
        return "unknown";
    }
}
```

### 7. Aktualizace ChatOrchestrator s Langfuse

V `ChatOrchestrator.ProcessMessageAsync`, přidej na začátku:

```csharp
// Start Langfuse trace
var traceId = await _langfuseService.StartTraceAsync("chat_query", new Dictionary<string, object>
{
    ["user_message"] = userMessage,
    ["has_history"] = conversationHistory != null && conversationHistory.Any()
});

// ... existing code ...

// Before returning, log to Langfuse
await _langfuseService.LogGenerationAsync(
    traceId,
    "chat_completion",
    userMessage,
    response.Content ?? "",
    new Dictionary<string, object>
    {
        ["response_time"] = responseTime,
        ["message_length"] = (response.Content ?? "").Length
    });

await _langfuseService.EndTraceAsync(traceId);
```

Přidej dependency:
```csharp
private readonly ILangfuseService _langfuseService;

public ChatOrchestrator(
    ISemanticKernelService kernelService,
    IServiceProvider serviceProvider,
    ILangfuseService langfuseService,
    ILogger<ChatOrchestrator> logger)
{
    _kernelService = kernelService;
    _serviceProvider = serviceProvider;
    _langfuseService = langfuseService;
    _logger = logger;
}
```

### 8. Registrace v Program.cs

```csharp
// Register HttpClient for Langfuse
builder.Services.AddHttpClient("Langfuse", client =>
{
    var baseUrl = builder.Configuration["Langfuse:BaseUrl"] ?? "http://localhost:3030";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register Langfuse service
builder.Services.AddScoped<ILangfuseService, LangfuseService>();
```

## Testování

### 1. Ověř, že Langfuse běží

```bash
curl http://localhost:3030/api/health

# Otevři v prohlížeči:
# http://localhost:3030
```

### 2. Test tracing

Nahraj dokument a sleduj v Langfuse UI:

```bash
curl -X POST http://localhost:5000/api/transactions/1/attachments \
  -F "file=@test-invoice.md"
```

V Langfuse UI by ses měl vidět:
- Trace "categorize_document"
- Generace s inputem (sample dokumentu)
- Outputem (kategorie)

### 3. Test chat s tracingem

```bash
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Kolik transakcí máme?"
  }'
```

V Langfuse UI:
- Trace "chat_query"
- Generation s user message
- Response

### 4. Prohlédni Langfuse Dashboard

V http://localhost:3030:

- **Traces**: Zobraz všechny traces
- **Generations**: Detail jednotlivých LLM calls
- **Analytics**: Statistiky (počet callů, latence, atd.)
- **Sessions**: Seskupené konverzace

## Užitečné Langfuse views

### Trace detail

V Langfuse můžeš vidět:
- Celkovou dobu trvání
- Všechny generace v rámci trace
- Input/output každé generace
- Metadata (model, tokens, custom data)
- Timeline events

### Analytics

- Počet requestů za den
- Průměrná latence
- Nejčastější typy traces
- Error rate

## Ověření

Po dokončení:

1. ✅ Langfuse běží a je dostupný na http://localhost:3030
2. ✅ API klíče jsou nakonfigurovány
3. ✅ Všechny LLM cally jsou logovány (categorization, embeddings, chat)
4. ✅ Traces jsou viditelné v Langfuse UI
5. ✅ Metadata jsou správně připojena
6. ✅ Můžeš filtrovat traces podle typu

## Docker Compose v3 - Přidání Langfuse

Aktualizuj **docker-compose.yml** o Langfuse službu:

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
      # Init script pro vytvoření Langfuse databáze
      - ./init-langfuse-db.sql:/docker-entrypoint-initdb.d/init-langfuse-db.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U appuser -d transactionsdb"]
      interval: 10s
      timeout: 5s
      retries: 5

  qdrant:
    image: qdrant/qdrant:latest
    container_name: qdrant-transactions
    ports:
      - "6333:6333"
      - "6334:6334"
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
        echo "Pulling models..."
        ollama pull all-minilm:l6-v2
        ollama pull llama3.1:8b
        echo "Models pulled successfully"
    restart: "no"

  langfuse:
    image: langfuse/langfuse:latest
    container_name: langfuse-monitoring
    depends_on:
      postgres:
        condition: service_healthy
    ports:
      - "3030:3000"
    environment:
      - DATABASE_URL=postgresql://appuser:AppPassword123@postgres:5432/langfusedb
      - NEXTAUTH_URL=http://localhost:3030
      - NEXTAUTH_SECRET=mysecretkey123456789abcdefghijklmnop
      - SALT=mysaltkey123456789abcdefghijklmnop
      - ENCRYPTION_KEY=0000000000000000000000000000000000000000000000000000000000000000
      - TELEMETRY_ENABLED=false
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000/api/public/health"]
      interval: 30s
      timeout: 10s
      retries: 5

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
      - Langfuse__BaseUrl=http://langfuse:3000
      - Langfuse__PublicKey=${LANGFUSE_PUBLIC_KEY:-pk-lf-dev}
      - Langfuse__SecretKey=${LANGFUSE_SECRET_KEY:-sk-lf-dev}
    ports:
      - "5000:5000"
    depends_on:
      postgres:
        condition: service_healthy
      qdrant:
        condition: service_healthy
      ollama:
        condition: service_healthy
      langfuse:
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

### Init SQL pro Langfuse databázi

**init-langfuse-db.sql** (v root složce projektu):
```sql
-- Create Langfuse database if not exists
SELECT 'CREATE DATABASE langfusedb'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'langfusedb')\gexec
```

### Environment variables pro Langfuse API keys

**.env** (v root složce projektu):
```bash
# Langfuse API Keys
# Po prvním spuštění Langfuse získáš tyto klíče z UI (Settings → API Keys)
LANGFUSE_PUBLIC_KEY=pk-lf-your-public-key-here
LANGFUSE_SECRET_KEY=sk-lf-your-secret-key-here
```

Aktualizuj docker-compose.yml reference:
```bash
docker-compose --env-file .env up
```

### První spuštění s Langfuse

```bash
# 1. Zastavení starého stacku
docker-compose down

# 2. Vytvoř init-langfuse-db.sql soubor (viz výše)

# 3. Build a spuštění s Langfuse
docker-compose up --build

# 4. Počkej až Langfuse naběhne (sleduj logy)
docker-compose logs -f langfuse

# 5. Otevři Langfuse UI
open http://localhost:3030

# 6. Zaregistruj se (první uživatel = admin)
# 7. Vytvoř projekt "Transaction Management"
# 8. Získej API klíče z Settings → API Keys
# 9. Aktualizuj .env soubor s klíči
# 10. Restart backend pro načtení nových klíčů
docker-compose restart backend
```

### Přístup k Langfuse Dashboard

- **Langfuse UI**: http://localhost:3030
- **Langfuse API**: http://localhost:3030/api

### Ověření Langfuse integrace

```bash
# Test embeddings (měl by vytvořit trace v Langfuse)
curl -X POST http://localhost:5000/api/test/embedding \
  -H "Content-Type: application/json" \
  -d '"Test embedding with Langfuse"'

# Test chat (měl by vytvořit trace v Langfuse)
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{"message": "Kolik transakcí máme?"}'

# Zkontroluj Langfuse UI
# → Měly by se zobrazit traces pro embedding a chat
```

### Troubleshooting

**Langfuse nezobrazuje traces:**
```bash
# 1. Zkontroluj API klíče v backendu
docker-compose exec backend printenv | grep LANGFUSE

# 2. Zkontroluj Langfuse logy
docker-compose logs langfuse

# 3. Ověř že backend může dosáhnout Langfuse
docker-compose exec backend curl -f http://langfuse:3000/api/public/health

# 4. Restart backend
docker-compose restart backend
```

**Langfuse databáze neexistuje:**
```bash
# Manuální vytvoření databáze
docker-compose exec postgres psql -U appuser -c "CREATE DATABASE langfusedb;"
docker-compose restart langfuse
```

**Langfuse health check fails:**
```bash
# Zkontroluj connection string
docker-compose logs langfuse | grep DATABASE

# Zkontroluj PostgreSQL
docker-compose exec postgres psql -U appuser -l
```

### Monitoring & Debug

```bash
# Sledování všech logů
docker-compose logs -f

# Pouze Langfuse logy
docker-compose logs -f langfuse

# Restart všech služeb
docker-compose restart

# Čištění včetně Langfuse dat
docker-compose down -v
```

### Production Notes

Pro production použij silnější secret keys:
```bash
# Vygeneruj NEXTAUTH_SECRET (min 32 znaků)
openssl rand -base64 32

# Vygeneruj SALT (min 32 znaků)
openssl rand -base64 32

# Vygeneruj ENCRYPTION_KEY (64 hex znaků)
openssl rand -hex 32
```

## Výstup této fáze

✅ Langfuse service pro tracing
✅ Integrace s EmbeddingService
✅ Integrace s LLMService (categorization)
✅ Integrace s ChatOrchestrator
✅ Dashboard pro monitoring
✅ Analytics a statistiky
✅ Debugging možnosti
✅ **Docker Compose s Langfuse službou**
✅ **Sdílené PostgreSQL pro app + Langfuse**
✅ **Automatické DB init pro Langfuse**
✅ **Environment variables pro API keys**

## Další krok

→ **14_document_generator.md** - Generování vzorových Markdown dokumentů (faktury, smlouvy, objednávky)
