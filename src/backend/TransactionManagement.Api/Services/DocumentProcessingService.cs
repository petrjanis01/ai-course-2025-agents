using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.Entities;
using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Services;

public interface IDocumentProcessingService
{
    Task ProcessAttachmentAsync(Guid attachmentId);
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

    public async Task ProcessAttachmentAsync(Guid attachmentId)
    {
        using var scope = _serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var chunkingService = scope.ServiceProvider.GetRequiredService<IChunkingService>();
        var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataEnrichmentService>();
        var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();
        var llmService = scope.ServiceProvider.GetRequiredService<ILLMService>();

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
            attachment.ProcessingStatus = ProcessingStatus.Processing;
            await context.SaveChangesAsync();

            // 2. Read file content
            var (fileBytes, _) = await fileStorage.GetFileAsync(attachment.FilePath);
            var content = System.Text.Encoding.UTF8.GetString(fileBytes);

            // 3. Categorize document using LLM
            var category = await llmService.CategorizeDocumentAsync(content);
            attachment.Category = ParseCategory(category);
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
                    Category = attachment.Category.ToString(),
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
            attachment.ProcessingStatus = ProcessingStatus.Completed;
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
                attachment.ProcessingStatus = ProcessingStatus.Failed;
                await context.SaveChangesAsync();
            }
        }
    }

    private DocumentCategory ParseCategory(string category)
    {
        return category.ToLower() switch
        {
            "invoice" => DocumentCategory.Invoice,
            "contract" => DocumentCategory.Contract,
            "purchase_order" => DocumentCategory.PurchaseOrder,
            _ => DocumentCategory.Unknown
        };
    }
}
