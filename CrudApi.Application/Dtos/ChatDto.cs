namespace CrudApi.Application.Dtos;

public class ChatPromptRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
}

public class ChatPromptResponse
{
    public string? ConversationId { get; set; }
    public List<ChatMessageResponse> Messages { get; set; } = new();
}

public class ChatMessageResponse
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ProductResponse? AffectedProduct { get; set; }
    public string? ProcessedAction { get; set; }
}
