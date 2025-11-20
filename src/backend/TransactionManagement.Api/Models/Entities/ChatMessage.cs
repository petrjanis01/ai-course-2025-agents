namespace TransactionManagement.Api.Models.Entities;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Role { get; set; } = string.Empty; // "user" or "assistant"

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid ChatSessionId { get; set; }

    // Navigation property
    public ChatSession ChatSession { get; set; } = null!;
}
