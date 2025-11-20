using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Models.Entities;

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public DocumentCategory Category { get; set; } = DocumentCategory.Unknown;

    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public Guid TransactionId { get; set; }

    // Navigation property
    public Transaction Transaction { get; set; } = null!;
}
