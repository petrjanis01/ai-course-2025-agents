# 06 - Attachments REST API

## Cíl
Implementovat REST API pro nahrávání a správu příloh k transakcím (upload, download, delete).

## Prerekvizity
- Dokončený krok 05 (Frontend transakcí)
- Běžící PostgreSQL databáze

## Kroky implementace

### 1. File Storage Service

**Services/FileStorageService.cs:**
```csharp
namespace TransactionManagement.Api.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, Guid transactionId);
    Task<(byte[] fileContent, string contentType)> GetFileAsync(string filePath);
    Task DeleteFileAsync(string filePath);
}

public class FileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IConfiguration configuration,
        ILogger<FileStorageService> logger)
    {
        _basePath = configuration["FileStorage:BasePath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "attachments");
        _logger = logger;

        // Ensure directory exists
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> SaveFileAsync(IFormFile file, Guid transactionId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        // Create transaction-specific directory
        var transactionDir = Path.Combine(_basePath, transactionId.ToString());
        if (!Directory.Exists(transactionDir))
        {
            Directory.CreateDirectory(transactionDir);
        }

        // Generate unique filename
        var fileExtension = Path.GetExtension(file.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(transactionDir, uniqueFileName);

        // Save file
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        _logger.LogInformation("File saved: {FilePath}", filePath);

        return filePath;
    }

    public async Task<(byte[] fileContent, string contentType)> GetFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        var fileContent = await File.ReadAllBytesAsync(filePath);
        var contentType = GetContentType(filePath);

        return (fileContent, contentType);
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

    private static string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }
}
```

### 2. Registrace služby v Program.cs

**Program.cs:**
```csharp
// Přidej před builder.Build():
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
```

### 3. Attachments Controller

**Controllers/AttachmentsController.cs:**
```csharp
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
    /// GET /api/transactions/{transactionId}/attachments/{id} - Vrátí detail přílohy
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AttachmentDto>> GetAttachment(
        Guid transactionId,
        Guid id)
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
    public async Task<IActionResult> DeleteAttachment(
        Guid transactionId,
        Guid id)
    {
        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == id && a.TransactionId == transactionId);

        if (attachment == null)
        {
            return NotFound(new { message = $"Attachment with ID {id} not found" });
        }

        // Delete file from storage
        await _fileStorage.DeleteFileAsync(attachment.FilePath);

        // Delete from database
        _context.Attachments.Remove(attachment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Attachment deleted: ID={AttachmentId}", id);

        return NoContent();
    }
}
```

### 4. Aktualizace TransactionDetailDto

Ověř, že **Models/DTOs/AttachmentDto.cs** existuje z kroku 02:

```csharp
using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Models.DTOs;

public class AttachmentDto
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
```

### 5. Konfigurace multipart form data

**Program.cs** - přidej konfiguraci pro upload:

```csharp
// Před builder.Build():
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});
```

Přidej using:
```csharp
using Microsoft.AspNetCore.Http.Features;
```

## Testování API

### 1. Pomocí Swagger UI

Spusť aplikaci:
```bash
dotnet run
```

Otevři Swagger UI: http://localhost:5000/swagger

### 2. Testovací scénář

**Krok 1: Vytvoř transakci (pokud nemáš)**
```bash
curl -X POST http://localhost:5000/api/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "description": "Transakce s přílohami",
    "amount": 10000,
    "companyId": "12345678",
    "companyName": "Test s.r.o.",
    "transactionType": 1,
    "transactionDate": "2025-01-15T00:00:00Z"
  }'
```

Zapamatuj si vrácené `id` (GUID).

**Krok 2: Nahraj přílohu**

Vytvoř testovací soubor `test.txt`:
```bash
echo "Test dokument" > test.txt
```

Nahraj soubor (nahraď TRANSACTION_GUID):
```bash
curl -X POST http://localhost:5000/api/transactions/TRANSACTION_GUID/attachments \
  -F "file=@test.txt"
```

**Krok 3: Seznam příloh**
```bash
curl http://localhost:5000/api/transactions/TRANSACTION_GUID/attachments
```

**Krok 4: Stáhni přílohu** (nahraď ATTACHMENT_GUID):
```bash
curl -OJ http://localhost:5000/api/transactions/TRANSACTION_GUID/attachments/ATTACHMENT_GUID/download
```

**Krok 5: Smaž přílohu**:
```bash
curl -X DELETE http://localhost:5000/api/transactions/TRANSACTION_GUID/attachments/ATTACHMENT_GUID
```

### 3. Test přes Swagger UI

1. **Upload souboru:**
   - Otevři `POST /api/transactions/{transactionId}/attachments`
   - Zadej transaction ID
   - Klikni "Choose File" a vyber soubor (PDF, JPG, TXT)
   - Execute

2. **Seznam příloh:**
   - Otevři `GET /api/transactions/{transactionId}/attachments`
   - Zadej transaction ID
   - Execute
   - Ověř, že vidíš nahrané přílohy

3. **Download:**
   - Otevři `GET /api/transactions/{transactionId}/attachments/{id}/download`
   - Zadej transaction ID a attachment ID
   - Execute
   - Soubor by se měl stáhnout

4. **Detail transakce s počtem příloh:**
   - Otevři `GET /api/transactions/{id}`
   - Ověř, že `attachmentCount` odpovídá počtu nahraných souborů

## Ověření

Po dokončení:

1. ✅ Lze nahrát soubor k transakci
2. ✅ Soubor se uloží do `data/attachments/{transactionId}/`
3. ✅ Attachment záznam se vytvoří v databázi
4. ✅ Lze získat seznam příloh transakce
5. ✅ Lze stáhnout soubor přílohy
6. ✅ Lze smazat přílohu (včetně souboru)
7. ✅ AttachmentCount v TransactionDto je správný
8. ✅ Validace velikosti souboru funguje (max 10 MB)
9. ✅ ProcessingStatus je defaultně Pending

## Výstup této fáze

✅ AttachmentsController s upload/download/delete operacemi
✅ FileStorageService pro práci se soubory
✅ POST /api/transactions/{id}/attachments - upload
✅ GET /api/transactions/{id}/attachments - seznam
✅ GET /api/transactions/{id}/attachments/{id} - detail
✅ GET /api/transactions/{id}/attachments/{id}/download - download
✅ DELETE /api/transactions/{id}/attachments/{id} - smazání
✅ Soubory ukládány do filesystému
✅ Metadata v databázi

## Další krok

→ **07_frontend_attachments.md** - Frontend pro nahrávání a správu příloh k transakcím
