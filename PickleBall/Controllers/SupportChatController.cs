using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.SupportChat;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/support-chat")]
[Authorize]
public class SupportChatController : ControllerBase
{
    private readonly ISupportChatService _supportChatService;

    public SupportChatController(ISupportChatService supportChatService)
    {
        _supportChatService = supportChatService;
    }

    [HttpGet("my-conversation")]
    public async Task<ActionResult<SupportConversationMessagesDto>> GetMyConversation([FromQuery] SupportMessageQuery query)
    {
        var userId = GetUserId();
        var result = await _supportChatService.GetMyConversationMessagesAsync(userId, query);
        return Ok(result);
    }

    [HttpPost("my-conversation/messages")]
    public async Task<ActionResult<SupportMessageDto>> SendMyMessage([FromBody] SendSupportMessageRequest request)
    {
        try
        {
            var userId = GetUserId();
            var role = GetUserRole();
            var message = await _supportChatService.SendMessageAsync(userId, role, userId, request.Content);
            return Ok(message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("my-conversation/read")]
    public async Task<ActionResult> MarkMyConversationRead()
    {
        var userId = GetUserId();
        var role = GetUserRole();
        await _supportChatService.MarkConversationReadAsync(userId, role, userId);
        return Ok(new { message = "Conversation marked as read" });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

    private string GetUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }
}
