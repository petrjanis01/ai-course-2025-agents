namespace TransactionManagement.Api.Models.Entities;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
