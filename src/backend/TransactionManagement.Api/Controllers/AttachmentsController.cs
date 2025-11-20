using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.DTOs;
using TransactionManagement.Api.Models.Entities;
using TransactionManagement.Api.Models.Enums;
using TransactionManagement.Api.Services;

namespace TransactionManagement.Api.Controllers;

[ApiController]
[Route("api/transactions/{transactionId}/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly ILogger<AttachmentsController> _logger;

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

    /// <summary>
    /// GET /api/transactions/{transactionId}/attachments - Vrátí seznam příloh transakce
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AttachmentDto>>> GetAttachments(Guid transactionId)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {transactionId} not found" });
        }

        var attachments = transaction.Attachments.Select(a => new AttachmentDto
        {
            Id = a.Id,
            TransactionId = a.TransactionId,
            FileName = a.FileName,
            Category = a.Category,
            ProcessingStatus = a.ProcessingStatus,
            CreatedAt = a.CreatedAt,
            ProcessedAt = a.ProcessedAt
        }).ToList();

        return Ok(attachments);
    }

    /// <summary>
    /// POST /api/transactions/{transactionId}/attachments - Nahraje novou přílohu
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AttachmentDto>> UploadAttachment(
        Guid transactionId,
        [FromForm] IFormFile file)
    {
        // Verify transaction exists
        var transaction = await _context.Transactions.FindAsync(transactionId);
        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {transactionId} not found" });
        }

        // Validate file
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "File is required" });
        }

        // Check file size (max 10 MB)
        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { message = "File size must not exceed 10 MB" });
        }

        // Save file
        var filePath = await _fileStorage.SaveFileAsync(file, transactionId);

        // Create attachment entity
        var attachment = new Attachment
        {
            TransactionId = transactionId,
            FileName = file.FileName,
            FilePath = filePath,
            Category = DocumentCategory.Unknown,
            ProcessingStatus = ProcessingStatus.Pending
        };

        _context.Attachments.Add(attachment);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Attachment uploaded: ID={AttachmentId}, TransactionId={TransactionId}, FileName={FileName}",
            attachment.Id, transactionId, file.FileName);

        // Queue for background processing
        var attachmentId = attachment.Id;
        await _backgroundTaskQueue.QueueBackgroundWorkItemAsync(async ct =>
        {
            var scope = HttpContext.RequestServices.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
            await processingService.ProcessAttachmentAsync(attachmentId);
        });

        _logger.LogInformation("Queued attachment {AttachmentId} for processing", attachmentId);

        var dto = new AttachmentDto
        {
            Id = attachment.Id,
            TransactionId = attachment.TransactionId,
            FileName = attachment.FileName,
            Category = attachment.Category,
            ProcessingStatus = attachment.ProcessingStatus,
            CreatedAt = attachment.CreatedAt,
            ProcessedAt = attachment.ProcessedAt
        };

        return CreatedAtAction(
            nameof(GetAttachment),
            new { transactionId, id = attachment.Id },
            dto);
    }

    /// <summary>
    /// GET /api/transactions/{transactionId}/attachments/{id} - Vrátí metadata přílohy
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AttachmentDto>> GetAttachment(Guid transactionId, Guid id)
    {
        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == id && a.TransactionId == transactionId);

        if (attachment == null)
        {
            return NotFound(new { message = $"Attachment with ID {id} not found" });
        }

        var dto = new AttachmentDto
        {
            Id = attachment.Id,
            TransactionId = attachment.TransactionId,
            FileName = attachment.FileName,
            Category = attachment.Category,
            ProcessingStatus = attachment.ProcessingStatus,
            CreatedAt = attachment.CreatedAt,
            ProcessedAt = attachment.ProcessedAt
        };

        return Ok(dto);
    }

    /// <summary>
    /// GET /api/transactions/{transactionId}/attachments/{id}/download - Stáhne soubor přílohy
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadAttachment(
        Guid transactionId,
        Guid id)
    {
        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == id && a.TransactionId == transactionId);

        if (attachment == null)
        {
            return NotFound(new { message = $"Attachment with ID {id} not found" });
        }

        try
        {
            var (fileContent, contentType) = await _fileStorage.GetFileAsync(attachment.FilePath);
            return File(fileContent, contentType, attachment.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "File not found on disk" });
        }
    }

    /// <summary>
    /// DELETE /api/transactions/{transactionId}/attachments/{id} - Smaže přílohu
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAttachment(Guid transactionId, Guid id)
    {
        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == id && a.TransactionId == transactionId);

        if (attachment == null)
        {
            return NotFound(new { message = $"Attachment with ID {id} not found" });
        }

        // Delete file from disk
        await _fileStorage.DeleteFileAsync(attachment.FilePath);

        // Delete from database
        _context.Attachments.Remove(attachment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Attachment deleted: ID={AttachmentId}", id);

        return NoContent();
    }
}
