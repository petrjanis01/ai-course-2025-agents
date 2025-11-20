using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.DTOs;
using TransactionManagement.Api.Models.Entities;
using TransactionManagement.Api.Services;

namespace TransactionManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        ILogger<TransactionsController> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/transactions - Vrátí seznam všech transakcí
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TransactionDto>>> GetTransactions()
    {
        var transactions = await _context.Transactions
            .Include(t => t.Attachments)
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Description = t.Description,
                Amount = t.Amount,
                CompanyId = t.CompanyId,
                CompanyName = t.CompanyName,
                TransactionType = t.TransactionType,
                TransactionDate = t.TransactionDate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                AttachmentCount = t.Attachments.Count
            })
            .ToListAsync();

        return Ok(transactions);
    }

    /// <summary>
    /// GET /api/transactions/{id} - Vrátí detail transakce včetně příloh
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionDetailDto>> GetTransaction(Guid id)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {id} not found" });
        }

        var dto = new TransactionDetailDto
        {
            Id = transaction.Id,
            Description = transaction.Description,
            Amount = transaction.Amount,
            CompanyId = transaction.CompanyId,
            CompanyName = transaction.CompanyName,
            TransactionType = transaction.TransactionType,
            TransactionDate = transaction.TransactionDate,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
            AttachmentCount = transaction.Attachments.Count,
            Attachments = transaction.Attachments.Select(a => new AttachmentDto
            {
                Id = a.Id,
                TransactionId = a.TransactionId,
                FileName = a.FileName,
                Category = a.Category,
                ProcessingStatus = a.ProcessingStatus,
                CreatedAt = a.CreatedAt,
                ProcessedAt = a.ProcessedAt
            }).ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// POST /api/transactions - Vytvoří novou transakci
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TransactionDto>> CreateTransaction(
        [FromBody] CreateTransactionDto dto)
    {
        var transaction = new Transaction
        {
            Description = dto.Description,
            Amount = dto.Amount,
            CompanyId = dto.CompanyId,
            CompanyName = dto.CompanyName,
            TransactionType = dto.TransactionType,
            TransactionDate = dto.TransactionDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Transaction created: ID={TransactionId}", transaction.Id);

        var result = new TransactionDto
        {
            Id = transaction.Id,
            Description = transaction.Description,
            Amount = transaction.Amount,
            CompanyId = transaction.CompanyId,
            CompanyName = transaction.CompanyName,
            TransactionType = transaction.TransactionType,
            TransactionDate = transaction.TransactionDate,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
            AttachmentCount = 0
        };

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, result);
    }

    /// <summary>
    /// PUT /api/transactions/{id} - Aktualizuje transakci
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TransactionDto>> UpdateTransaction(
        Guid id,
        [FromBody] UpdateTransactionDto dto)
    {
        var transaction = await _context.Transactions.FindAsync(id);

        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {id} not found" });
        }

        transaction.Description = dto.Description;
        transaction.Amount = dto.Amount;
        transaction.CompanyId = dto.CompanyId;
        transaction.CompanyName = dto.CompanyName;
        transaction.TransactionType = dto.TransactionType;
        transaction.TransactionDate = dto.TransactionDate;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Transaction updated: ID={TransactionId}", transaction.Id);

        var result = new TransactionDto
        {
            Id = transaction.Id,
            Description = transaction.Description,
            Amount = transaction.Amount,
            CompanyId = transaction.CompanyId,
            CompanyName = transaction.CompanyName,
            TransactionType = transaction.TransactionType,
            TransactionDate = transaction.TransactionDate,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
            AttachmentCount = await _context.Attachments.CountAsync(a => a.TransactionId == id)
        };

        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/transactions/{id} - Smaže transakci včetně příloh
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {id} not found" });
        }

        // Delete all attachment files from filesystem
        foreach (var attachment in transaction.Attachments)
        {
            await _fileStorage.DeleteFileAsync(attachment.FilePath);
        }

        // Delete from database (cascade delete attachments)
        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Transaction deleted: ID={TransactionId}", id);

        return NoContent();
    }
}
