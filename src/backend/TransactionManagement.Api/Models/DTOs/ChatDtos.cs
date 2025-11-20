namespace TransactionManagement.Api.Models.DTOs;

public class ChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public List<ChatMessageDto>? ConversationHistory { get; set; }
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty; // "user" nebo "assistant"
    public string Content { get; set; } = string.Empty;
}

public class ChatResponseDto
{
    public string Message { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public ChatMetadataDto? Metadata { get; set; }
}

public class ChatMetadataDto
{
    public int TokensUsed { get; set; }
    public double ResponseTime { get; set; }
    public List<string> AgentsUsed { get; set; } = new();
}
