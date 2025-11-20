# 06 - Document Processing Agent

## Cíl
Implementovat chunking service, metadata enrichment a DocumentProcessingAgent pro automatické zpracování nahraných dokumentů v background.

## Prerekvizity
- Dokončený krok 10 (Qdrant a embeddings)
- Microsoft Semantic Kernel (už nainstalováno)

## Kroky implementace

### 1. Chunking Service

**Services/ChunkingService.cs:**
```csharp
using System.Text.RegularExpressions;

namespace TransactionManagement.Api.Services;

public interface IChunkingService
{
    List<TextChunk> SplitIntoChunks(string text, int targetTokens = 800, int overlapTokens = 100);
}

public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;

    public ChunkingService(ILogger<ChunkingService> logger)
    {
        _logger = logger;
    }

    public List<TextChunk> SplitIntoChunks(string text, int targetTokens = 800, int overlapTokens = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<TextChunk>();
        }

        // Split text into sentences
        var sentences = SplitIntoSentences(text);
        var chunks = new List<TextChunk>();
        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokenCount(sentence);

            // If adding this sentence would exceed target, save current chunk
            if (currentTokenCount + sentenceTokens > targetTokens && currentChunk.Any())
            {
                var chunkText = string.Join(" ", currentChunk);
                chunks.Add(new TextChunk
                {
                    Content = chunkText,
                    TokenCount = currentTokenCount
                });

                // Create overlap: keep last few sentences
                var overlapSentences = GetOverlapSentences(currentChunk, overlapTokens);
                currentChunk = overlapSentences.ToList();
                currentTokenCount = overlapSentences.Sum(EstimateTokenCount);
            }

            currentChunk.Add(sentence);
            currentTokenCount += sentenceTokens;
        }

        // Add remaining chunk
        if (currentChunk.Any())
        {
            var chunkText = string.Join(" ", currentChunk);
            chunks.Add(new TextChunk
            {
                Content = chunkText,
                TokenCount = currentTokenCount
            });
        }

        _logger.LogInformation("Split text into {ChunkCount} chunks (avg {AvgTokens} tokens each)",
            chunks.Count,
            chunks.Any() ? chunks.Average(c => c.TokenCount) : 0);

        return chunks;
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Split by sentence-ending punctuation followed by space/newline
        var pattern = @"(?<=[.!?])\s+(?=[A-ZÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ])";
        var sentences = Regex.Split(text, pattern)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }

    private int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token
        return text.Length / 4;
    }

    private List<string> GetOverlapSentences(List<string> sentences, int targetOverlapTokens)
    {
        var overlap = new List<string>();
        var overlapTokenCount = 0;

        // Take sentences from the end until we reach target overlap
        for (int i = sentences.Count - 1; i >= 0; i--)
        {
            var sentence = sentences[i];
            var sentenceTokens = EstimateTokenCount(sentence);

            if (overlapTokenCount + sentenceTokens > targetOverlapTokens && overlap.Any())
            {
                break;
            }

            overlap.Insert(0, sentence);
            overlapTokenCount += sentenceTokens;
        }

        return overlap;
    }
}

public class TextChunk
{
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
}
```

### 2. Metadata Enrichment Service

**Services/MetadataEnrichmentService.cs:**
```csharp
using System.Text.RegularExpressions;

namespace TransactionManagement.Api.Services;

public interface IMetadataEnrichmentService
{
    DocumentMetadata EnrichMetadata(string content);
}

public class MetadataEnrichmentService : IMetadataEnrichmentService
{
    // Regex patterns
    private static readonly Regex AmountPattern = new(@"\d+[\s,]*\d*\s*(Kč|EUR|CZK|€|\$)", RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"\d{1,2}\.\s*\d{1,2}\.\s*\d{4}", RegexOptions.Compiled);
    private static readonly Regex IsoDatePattern = new(@"\d{4}-\d{2}-\d{2}", RegexOptions.Compiled);

    public DocumentMetadata EnrichMetadata(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new DocumentMetadata();
        }

        var hasAmounts = AmountPattern.IsMatch(content);
        var hasDates = DatePattern.IsMatch(content) || IsoDatePattern.IsMatch(content);
        var wordCount = CountWords(content);

        return new DocumentMetadata
        {
            HasAmounts = hasAmounts,
            HasDates = hasDates,
            WordCount = wordCount
        };
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}

public class DocumentMetadata
{
    public bool HasAmounts { get; set; }
    public bool HasDates { get; set; }
    public int WordCount { get; set; }
}
```

### 3. Background Task Queue

**Services/BackgroundTaskQueue.cs:**
```csharp
using System.Threading.Channels;

namespace TransactionManagement.Api.Services;

public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem);
    ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
}

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);
    }

    public async ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }

        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}
```

**Services/QueuedHostedService.cs:**
```csharp
namespace TransactionManagement.Api.Services;

public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(
        IBackgroundTaskQueue taskQueue,
        ILogger<QueuedHostedService> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queued Hosted Service is running");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                await workItem(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing background work item");
            }
        }

        _logger.LogInformation("Queued Hosted Service is stopping");
    }
}
```

### 4. Document Processing Service

**Services/DocumentProcessingService.cs:**
```csharp
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.Entities;

namespace TransactionManagement.Api.Services;

public interface IDocumentProcessingService
{
    Task ProcessAttachmentAsync(int attachmentId);
}

public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        IServiceProvider serviceProvider,
        ILogger<DocumentProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task ProcessAttachmentAsync(int attachmentId)
    {
        using var scope = _serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var chunkingService = scope.ServiceProvider.GetRequiredService<IChunkingService>();
        var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataEnrichmentService>();
        var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();
        var llmService = scope.ServiceProvider.GetRequiredService<ILLMService>(); // Will create this

        try
        {
            _logger.LogInformation("Processing attachment {AttachmentId}", attachmentId);

            // 1. Load attachment
            var attachment = await context.Attachments
                .Include(a => a.Transaction)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment == null)
            {
                _logger.LogWarning("Attachment {AttachmentId} not found", attachmentId);
                return;
            }

            // Update status
            attachment.ProcessingStatus = "processing";
            await context.SaveChangesAsync();

            // 2. Read file content
            string content;
            using (var stream = await fileStorage.GetFileAsync(attachment.FilePath))
            using (var reader = new StreamReader(stream))
            {
                content = await reader.ReadToEndAsync();
            }

            // 3. Categorize document using LLM
            var category = await llmService.CategorizeDocumentAsync(content);
            attachment.Category = category;
            await context.SaveChangesAsync();

            _logger.LogInformation("Document categorized as: {Category}", category);

            // 4. Split into chunks
            var chunks = chunkingService.SplitIntoChunks(content);

            _logger.LogInformation("Document split into {ChunkCount} chunks", chunks.Count);

            // 5. Process each chunk
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];

                // Enrich metadata
                var metadata = metadataService.EnrichMetadata(chunk.Content);

                // Index to Qdrant
                var documentChunk = new DocumentChunk
                {
                    AttachmentId = attachment.Id,
                    TransactionId = attachment.TransactionId,
                    ChunkIndex = i,
                    TotalChunks = chunks.Count,
                    Category = category,
                    FileName = attachment.FileName,
                    Content = chunk.Content,
                    TokenCount = chunk.TokenCount,
                    HasAmounts = metadata.HasAmounts,
                    HasDates = metadata.HasDates,
                    WordCount = metadata.WordCount
                };

                await qdrantService.IndexDocumentChunkAsync(documentChunk);
            }

            // 6. Mark as completed
            attachment.ProcessingStatus = "completed";
            attachment.ProcessedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            _logger.LogInformation("Attachment {AttachmentId} processed successfully", attachmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing attachment {AttachmentId}", attachmentId);

            // Mark as failed
            var attachment = await context.Attachments.FindAsync(attachmentId);
            if (attachment != null)
            {
                attachment.ProcessingStatus = "failed";
                await context.SaveChangesAsync();
            }
        }
    }
}
```

### 5. LLM Service (Simple version for categorization)

**Services/LLMService.cs:**
```csharp
using System.Text;
using System.Text.Json;

namespace TransactionManagement.Api.Services;

public interface ILLMService
{
    Task<string> CategorizeDocumentAsync(string content);
}

public class LLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<LLMService> _logger;

    public LLMService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LLMService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _model = configuration["LLM:Model"] ?? "llama3.1:8b";
        _logger = logger;
    }

    public async Task<string> CategorizeDocumentAsync(string content)
    {
        // Take first 2000 characters for categorization
        var sampleContent = content.Length > 2000 ? content.Substring(0, 2000) : content;

        var systemPrompt = @"You are a document classifier. Analyze the document and classify it into one of these categories:
- invoice (faktura)
- contract (smlouva)
- purchase_order (objednávka)
- unknown (if you cannot determine)

Respond with ONLY the category name, nothing else.";

        var userPrompt = $"Classify this document:\n\n{sampleContent}";

        var request = new
        {
            model = _model,
            prompt = $"{systemPrompt}\n\n{userPrompt}",
            stream = false,
            options = new
            {
                temperature = 0.1,
                num_predict = 20
            }
        };

        var json = JsonSerializer.Serialize(request);
        var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/api/generate", requestContent);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

            var category = result?.Response?.Trim().ToLower() ?? "unknown";

            // Validate category
            var validCategories = new[] { "invoice", "contract", "purchase_order", "unknown" };
            if (!validCategories.Contains(category))
            {
                _logger.LogWarning("Invalid category returned: {Category}, defaulting to unknown", category);
                category = "unknown";
            }

            return category;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error categorizing document");
            return "unknown";
        }
    }

    private class OllamaResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}
```

### 6. Aktualizace AttachmentsController

V `AttachmentsController.cs`, přidej do metody `UploadAttachment` po uložení souboru:

```csharp
// Queue for background processing
await _backgroundTaskQueue.QueueBackgroundWorkItemAsync(async ct =>
{
    var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
    await processingService.ProcessAttachmentAsync(attachment.Id);
});

_logger.LogInformation("Queued attachment {AttachmentId} for processing", attachment.Id);
```

Přidej dependency do constructoru:
```csharp
private readonly IBackgroundTaskQueue _backgroundTaskQueue;

public AttachmentsController(
    ApplicationDbContext context,
    IFileStorageService fileStorage,
    IBackgroundTaskQueue backgroundTaskQueue,
    ILogger<AttachmentsController> logger)
{
    _context = context;
    _fileStorage = fileStorage;
    _backgroundTaskQueue = backgroundTaskQueue;
    _logger = logger;
}
```

### 7. Registrace v Program.cs

```csharp
// Register services
builder.Services.AddScoped<IChunkingService, ChunkingService>();
builder.Services.AddScoped<IMetadataEnrichmentService, MetadataEnrichmentService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<ILLMService, LLMService>();

// Background task queue
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();
```

## Testování

### 1. Vytvoř test Markdown soubor

**test-invoice.md:**
```markdown
# FAKTURA

**Číslo faktury:** 2025-001
**Datum vystavení:** 15.1.2025
**Datum splatnosti:** 30.1.2025

## Dodavatel
ACME Corporation s.r.o.
IČO: 12345678

## Odběratel
Test Client Ltd.
IČO: 87654321

## Položky

| Popis | Množství | Cena | Celkem |
|-------|----------|------|--------|
| Software licence | 1 | 25000 Kč | 25000 Kč |

**Celkem k úhradě:** 25000 Kč
```

### 2. Nahraj dokument

```bash
curl -X POST http://localhost:5000/api/transactions/1/attachments \
  -F "file=@test-invoice.md"
```

### 3. Sleduj logy

V konzoli by ses měl vidět:
```
Processing attachment 1
Document categorized as: invoice
Document split into 1 chunks
Indexed chunk 0/1 for attachment 1
Attachment 1 processed successfully
```

### 4. Ověř v databázi

```bash
docker exec -it postgres-transactions psql -U transactionuser -d transactionsdb

SELECT "Id", "FileName", "Category", "ProcessingStatus", "ProcessedAt"
FROM "Attachments";
```

### 5. Ověř v Qdrantu

```bash
curl http://localhost:6333/collections/transaction_documents

# Měl bys vidět points_count: 1 (nebo více)
```

## Ověření

Po dokončení:

1. ✅ Nahraj Markdown dokument
2. ✅ Dokument je automaticky zpracován v pozadí
3. ✅ Kategorie je určena pomocí LLM
4. ✅ Dokument je rozdělen na chunky s překryvem
5. ✅ Metadata jsou obohacena (has_amounts, has_dates)
6. ✅ Všechny chunky jsou indexovány v Qdrantu
7. ✅ Status přílohy je "completed"

## Výstup této fáze

✅ ChunkingService s 800 tokenů + 100 overlap
✅ MetadataEnrichmentService (amounts, dates, word count)
✅ BackgroundTaskQueue pro asynchronní zpracování
✅ DocumentProcessingService s kompletním workflow
✅ LLM služba pro kategorizaci dokumentů
✅ Automatické zpracování při uploadu

## Další krok

→ **07_chat_agenti.md** - Implementace ChatOrchestrator, DatabaseAgent a DocumentSearchAgent
