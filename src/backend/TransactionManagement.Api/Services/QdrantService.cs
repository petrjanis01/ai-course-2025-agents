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
    Task DeleteDocumentAsync(Guid attachmentId);
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

        // Parse URL to get host and port
        var uri = new Uri(_options.Url);
        var host = uri.Host;
        var port = uri.Port;
        var useHttps = uri.Scheme == "https";

        _client = new QdrantClient(host, port, useHttps);
    }

    public async Task<bool> CollectionExistsAsync()
    {
        try
        {
            var collections = await _client.ListCollectionsAsync();
            return collections.Any(c => c == _options.CollectionName);
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

        // Create deterministic UUID for the point ID based on attachment ID and chunk index
        var pointIdSource = $"{chunk.AttachmentId:N}{chunk.ChunkIndex:D8}";
        var pointIdGuid = Guid.Parse(pointIdSource.Substring(0, 32));

        // Prepare payload
        var payload = new Dictionary<string, Value>
        {
            ["attachment_id"] = chunk.AttachmentId.ToString(),
            ["transaction_id"] = chunk.TransactionId.ToString(),
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
            Id = new PointId { Uuid = pointIdGuid.ToString() },
            Vectors = embedding,
            Payload = { payload }
        };

        await _client.UpsertAsync(
            collectionName: _options.CollectionName,
            points: new[] { point });

        _logger.LogDebug("Indexed chunk {ChunkIndex}/{TotalChunks} for attachment {AttachmentId} with point ID {PointId}",
            chunk.ChunkIndex, chunk.TotalChunks, chunk.AttachmentId, pointIdGuid);
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
                        Match = new Match { Keyword = filters.TransactionId.Value.ToString() }
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
            AttachmentId = Guid.Parse(r.Payload["attachment_id"].StringValue),
            TransactionId = Guid.Parse(r.Payload["transaction_id"].StringValue),
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

    public async Task DeleteDocumentAsync(Guid attachmentId)
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
                        Match = new Match { Keyword = attachmentId.ToString() }
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
    public Guid AttachmentId { get; set; }
    public Guid TransactionId { get; set; }
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
    public Guid AttachmentId { get; set; }
    public Guid TransactionId { get; set; }
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
    public Guid? TransactionId { get; set; }
    public bool? HasAmounts { get; set; }
    public bool? HasDates { get; set; }
}
