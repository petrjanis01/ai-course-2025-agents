using Microsoft.AspNetCore.Mvc;
using TransactionManagement.Api.Models.DTOs;
using TransactionManagement.Api.Services.Agents;

namespace TransactionManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatOrchestrator _chatOrchestrator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatOrchestrator chatOrchestrator,
        ILogger<ChatController> logger)
    {
        _chatOrchestrator = chatOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/chat/message - Process chat message
    /// </summary>
    [HttpPost("message")]
    [ProducesResponseType(typeof(ChatResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ChatResponseDto>> SendMessage([FromBody] ChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message cannot be empty" });
        }

        try
        {
            var response = await _chatOrchestrator.ProcessMessageAsync(
                request.Message,
                request.ConversationHistory);

            var sessionId = request.SessionId ?? Guid.NewGuid();

            return Ok(new ChatResponseDto
            {
                Message = response.Message,
                SessionId = sessionId,
                Metadata = new ChatMetadataDto
                {
                    TokensUsed = response.TokensUsed,
                    ResponseTime = response.ResponseTime,
                    AgentsUsed = response.AgentsUsed
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new
            {
                message = "Error processing message",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// GET /api/chat/health - Health check
    /// </summary>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
