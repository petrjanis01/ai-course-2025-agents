using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Services.Agents;

/// <summary>
/// Funkce pro práci s dokumenty a jejich sémantické vyhledávání.
/// Podporuje RAG (Retrieval Augmented Generation) workflow.
/// </summary>
public class DocumentFunctions
{
    private readonly IQdrantService _qdrantService;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DocumentFunctions> _logger;

    public DocumentFunctions(
        IQdrantService qdrantService,
        IFileStorageService fileStorage,
        ApplicationDbContext context,
        ILogger<DocumentFunctions> logger)
    {
        _qdrantService = qdrantService;
        _fileStorage = fileStorage;
        _context = context;
        _logger = logger;
    }

    [Description(@"Search for documents using semantic similarity via Qdrant vector database.
This is the PRIMARY function for finding documents based on meaning/context, not exact keywords.

Use cases:
- 'Najdi smlouvy o dodávkách' - finds contracts about deliveries
- 'Které dokumenty zmiňují výpovědní lhůtu?' - finds documents mentioning notice period
- 'Faktury od ACME' - finds invoices from ACME company

Returns: List of document chunks with similarity scores and content preview (300 chars).
If you need full document content, use GetDocumentContent() with the AttachmentId from results.")]
    public async Task<string> SearchDocuments(
        [Description("Search query describing what you're looking for (in Czech or English)")]
        string query,
        [Description("Document category filter: 'invoice', 'contract', 'purchase_order', or null for all categories")]
        string? category = null,
        [Description("Filter for documents containing monetary amounts (true/false/null)")]
        bool? hasAmounts = null,
        [Description("Filter for documents containing dates (true/false/null)")]
        bool? hasDates = null,
        [Description("Maximum number of results to return (default: 5, max: 20)")]
        int limit = 5)
    {
        _logger.LogInformation("=== SearchDocuments called ===");
        _logger.LogInformation("Query: '{Query}', Category: {Category}, Limit: {Limit}",
            query, category ?? "all", limit);

        var filters = new SearchFilters
        {
            Category = category,
            HasAmounts = hasAmounts,
            HasDates = hasDates
        };

        var results = await _qdrantService.SearchAsync(query, filters, limit);

        _logger.LogInformation("Found {Count} results", results.Count);

        var formattedResults = results.Select(r => new
        {
            AttachmentId = r.AttachmentId,
            TransactionId = r.TransactionId,
            FileName = r.FileName,
            Category = r.Category,
            Score = Math.Round(r.Score, 3),
            ChunkIndex = r.ChunkIndex,
            TotalChunks = r.TotalChunks,
            ContentPreview = r.Content.Length > 300
                ? r.Content.Substring(0, 300) + "..."
                : r.Content,
            HasAmounts = r.HasAmounts,
            HasDates = r.HasDates
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(formattedResults, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    [Description(@"Get full content of a specific document by its attachment ID.
Use this function as a FOLLOW-UP after SearchDocuments when you need:
- Complete document text (not just 300 char preview)
- Exact values, amounts, dates that may be outside the preview
- Full context to answer detailed questions

Typical workflow:
1. SearchDocuments() finds relevant document → returns AttachmentId (GUID)
2. GetDocumentContent(AttachmentId) retrieves full text
3. Analyze full text and provide detailed answer

Returns: Complete document content with metadata (filename, category, etc.)")]
    public async Task<string> GetDocumentContent(
        [Description("Attachment ID (GUID) of the document to retrieve (from SearchDocuments results)")]
        Guid attachmentId)
    {
        _logger.LogInformation("=== GetDocumentContent called ===");
        _logger.LogInformation("AttachmentId: {AttachmentId}", attachmentId);

        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null)
        {
            return $"Attachment with ID {attachmentId} not found.";
        }

        try
        {
            var (fileContent, _) = await _fileStorage.GetFileAsync(attachment.FilePath);
            using var stream = new MemoryStream(fileContent);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            var result = new
            {
                AttachmentId = attachment.Id,
                TransactionId = attachment.TransactionId,
                FileName = attachment.FileName,
                Category = attachment.Category.ToString(),
                Content = content
            };

            return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading document {AttachmentId}", attachmentId);
            return $"Error reading document: {ex.Message}";
        }
    }

    [Description(@"Get aggregated counts of documents grouped by category.
Use this for STATISTICAL queries, not for searching specific documents.

Use cases:
- 'Kolik máme faktur?' - How many invoices do we have?
- 'Jaké typy dokumentů máme?' - What types of documents do we have?
- 'Máme víc smluv nebo objednávek?' - Do we have more contracts or purchase orders?

Returns: JSON array with category names and their counts.
Only counts 'completed' documents (successfully processed).")]
    public async Task<string> CountDocumentsByCategory()
    {
        _logger.LogInformation("=== CountDocumentsByCategory called ===");

        var counts = await _context.Attachments
            .Where(a => a.ProcessingStatus == ProcessingStatus.Completed)
            .GroupBy(a => a.Category)
            .Select(g => new
            {
                Category = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync();

        _logger.LogInformation("Found {CategoryCount} categories with {TotalCount} total documents",
            counts.Count, counts.Sum(c => c.Count));

        return System.Text.Json.JsonSerializer.Serialize(counts, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
