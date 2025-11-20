using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Models.DTOs;

public class TransactionDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int AttachmentCount { get; set; }
}

public class TransactionDetailDto : TransactionDto
{
    public List<AttachmentDto> Attachments { get; set; } = new();
}

public class CreateTransactionDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class UpdateTransactionDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
}
