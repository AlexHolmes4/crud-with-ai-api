namespace CrudApi.Application.Dtos;

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatPromptRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
}

public class ChatPromptResponse
{
    public string? ConversationId { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
    public string? ProcessedAction { get; set; }
    public ProductResponse? AffectedProduct { get; set; }
}
