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
