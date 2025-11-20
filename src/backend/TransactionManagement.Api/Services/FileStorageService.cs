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
            ".md" => "text/markdown",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }
}
