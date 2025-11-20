# 03 - CRUD Operace pro Transakce a Přílohy

## Cíl
Implementovat REST API endpointy pro správu transakcí a příloh včetně file storage service.

## Prerekvizity
- Dokončený krok 02 (databáze a modely)
- Běžící PostgreSQL databáze

## Kroky implementace

### 1. File Storage Service

**Services/FileStorageService.cs:**
```csharp
namespace TransactionManagement.Api.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(int transactionId, int attachmentId, Stream fileStream, string fileName);
    Task<Stream> GetFileAsync(string filePath);
    Task DeleteFileAsync(string filePath);
    Task DeleteTransactionFolderAsync(int transactionId);
}

public class FileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IConfiguration configuration,
        ILogger<FileStorageService> logger)
    {
        _basePath = configuration["FileStorage:BasePath"] ?? "/app/data/attachments";
        _logger = logger;

        // Ensure base directory exists
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveFileAsync(
        int transactionId,
        int attachmentId,
        Stream fileStream,
        string fileName)
    {
        var transactionFolder = Path.Combine(_basePath, transactionId.ToString());
        Directory.CreateDirectory(transactionFolder);

        var extension = Path.GetExtension(fileName);
        var savedFileName = $"attachment-{attachmentId}{extension}";
        var filePath = Path.Combine(transactionFolder, savedFileName);

        using (var fileStreamOut = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fileStreamOut);
        }

        _logger.LogInformation("File saved: {FilePath}", filePath);
        return filePath;
    }

    public Task<Stream> GetFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        return Task.FromResult<Stream>(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task DeleteFileAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("File deleted: {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }

    public Task DeleteTransactionFolderAsync(int transactionId)
    {
        var transactionFolder = Path.Combine(_basePath, transactionId.ToString());

        if (Directory.Exists(transactionFolder))
        {
            Directory.Delete(transactionFolder, recursive: true);
            _logger.LogInformation("Transaction folder deleted: {Folder}", transactionFolder);
        }

        return Task.CompletedTask;
    }
}
```

### 2. Transactions Controller

**Controllers/TransactionsController.cs:**
```csharp
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
    public async Task<ActionResult<TransactionDetailDto>> GetTransaction(int id)
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
        int id,
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
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
        {
            return NotFound(new { message = $"Transaction with ID {id} not found" });
        }

        // Delete files from filesystem
        await _fileStorage.DeleteTransactionFolderAsync(id);

        // Delete from database (cascade delete attachments)
        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Transaction deleted: ID={TransactionId}", id);

        return NoContent();
    }
}
```

### 3. Attachments Controller

**Controllers/AttachmentsController.cs:**
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.DTOs;
using TransactionManagement.Api.Models.Entities;
using TransactionManagement.Api.Services;

namespace TransactionManagement.Api.Controllers;

[ApiController]
[Route("api/transactions/{transactionId}/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        ILogger<AttachmentsController> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/transactions/{transactionId}/attachments - Nahraje novou přílohu
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AttachmentDto>> UploadAttachment(
        int transactionId,
        IFormFile file)
    {
        // Validate transaction exists
        var transactionExists = await _context.Transactions.AnyAsync(t => t.Id == transactionId);
        if (!transactionExists)
        {
            return NotFound(new { message = $"Transaction with ID {transactionId} not found" });
        }

        // Validate file
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "File is required" });
        }

        // Validate Markdown file
        if (!file.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only Markdown (.md) files are allowed" });
        }

        // Create attachment record
        var attachment = new Attachment
        {
            TransactionId = transactionId,
            FileName = file.FileName,
            FilePath = "", // Will be set after file save
            ProcessingStatus = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Attachments.Add(attachment);
        await _context.SaveChangesAsync();

        // Save file to disk
        using (var stream = file.OpenReadStream())
        {
            var filePath = await _fileStorage.SaveFileAsync(
                transactionId,
                attachment.Id,
                stream,
                file.FileName);

            attachment.FilePath = filePath;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Attachment uploaded: ID={AttachmentId}, TransactionId={TransactionId}",
            attachment.Id,
            transactionId);

        // TODO: Queue for background processing (will be implemented in step 06)

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

        return CreatedAtAction(nameof(GetAttachment), new { transactionId, id = attachment.Id }, dto);
    }

    /// <summary>
    /// GET /api/transactions/{transactionId}/attachments/{id} - Vrátí metadata přílohy
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AttachmentDto>> GetAttachment(int transactionId, int id)
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
    /// GET /api/transactions/{transactionId}/attachments/{id}/download - Stáhne obsah přílohy
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadAttachment(int transactionId, int id)
    {
        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == id && a.TransactionId == transactionId);

        if (attachment == null)
        {
            return NotFound(new { message = $"Attachment with ID {id} not found" });
        }

        try
        {
            var stream = await _fileStorage.GetFileAsync(attachment.FilePath);
            return File(stream, "text/markdown", attachment.FileName);
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
    public async Task<IActionResult> DeleteAttachment(int transactionId, int id)
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
```

### 4. Registrace services v Program.cs

Přidej do `Program.cs` před `var app = builder.Build();`:

```csharp
// Register services
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
```

### 5. Aktualizace appsettings.json

```json
{
  "FileStorage": {
    "BasePath": "./data/attachments"
  }
}
```

## Testování API

### Pomocí Swagger UI

1. **Spusť aplikaci:**
```bash
dotnet run
```

2. **Otevři Swagger UI:**
   - http://localhost:5000/swagger

3. **Testuj endpointy:**

**Vytvoř transakci:**
```json
POST /api/transactions
{
  "description": "Test transakce",
  "amount": 15000.00,
  "companyId": "12345678",
  "companyName": "Test Company",
  "transactionType": "expense",
  "transactionDate": "2025-01-15T00:00:00Z"
}
```

**Nahraj přílohu:**
```
POST /api/transactions/1/attachments
Form-data: file = [vybrat .md soubor]
```

**Seznam transakcí:**
```
GET /api/transactions
```

**Detail transakce:**
```
GET /api/transactions/1
```

### Pomocí curl

```bash
# Vytvoř transakci
curl -X POST http://localhost:5000/api/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "description": "Nákup materiálu",
    "amount": 15000,
    "companyId": "12345678",
    "companyName": "ACME Corp",
    "transactionType": "expense",
    "transactionDate": "2025-01-15T00:00:00Z"
  }'

# Nahraj přílohu
curl -X POST http://localhost:5000/api/transactions/1/attachments \
  -F "file=@test-invoice.md"

# Seznam transakcí
curl http://localhost:5000/api/transactions

# Stáhni přílohu
curl http://localhost:5000/api/transactions/1/attachments/1/download -o downloaded.md
```

## Ověření

Po dokončení:

1. ✅ Vytvoř novou transakci přes API
2. ✅ Nahraj Markdown soubor jako přílohu
3. ✅ Stáhni přílohu
4. ✅ Aktualizuj transakci
5. ✅ Smaž přílohu
6. ✅ Smaž transakci (včetně všech příloh)
7. ✅ Ověř, že soubory jsou správně uloženy v `./data/attachments/{transactionId}/`

## Výstup této fáze

✅ FileStorageService pro správu souborů
✅ TransactionsController s CRUD operacemi
✅ AttachmentsController s upload/download
✅ Validace Markdown souborů
✅ Cascade delete (transakce → přílohy)
✅ Funkční REST API pro správu transakcí

## Další krok

→ **04_seed_data.md** - Generování 100 testovacích transakcí s realistickými daty
