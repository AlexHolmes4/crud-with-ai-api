using CrudApi.Application.Dtos;
using CrudApi.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CrudApi.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly IProductService _productService;

    public ConversationsController(
        IConversationService conversationService,
        IProductService productService)
    {
        _conversationService = conversationService;
        _productService = productService;
    }

    /// <summary>
    /// Process a user prompt through Claude to identify and execute product operations.
    /// </summary>
    [HttpPost("messages")]
    public async Task<ActionResult<ChatPromptResponse>> SendMessage(
        [FromBody] ChatPromptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return BadRequest(new { message = "Prompt is required" });

        var response = await _conversationService.ProcessPromptAsync(request, _productService, cancellationToken);
        return Ok(response);
    }
}
