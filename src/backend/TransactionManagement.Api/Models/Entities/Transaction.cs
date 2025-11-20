using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Models.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string CompanyId { get; set; } = string.Empty; // IČO

    public string? CompanyName { get; set; }

    public TransactionType TransactionType { get; set; }

    public DateTime TransactionDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
