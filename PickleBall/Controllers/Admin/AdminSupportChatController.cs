using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Products;
using PickleBall.Application.DTOs.SupportChat;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers.Admin;

[ApiController]
[Route("api/admin/support-chat")]
[Authorize(Roles = "Admin")]
public class AdminSupportChatController : ControllerBase
{
    private readonly ISupportChatService _supportChatService;

    public AdminSupportChatController(ISupportChatService supportChatService)
    {
        _supportChatService = supportChatService;
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<PagedResultDto<SupportConversationDto>>> GetConversations([FromQuery] SupportConversationQuery query)
    {
        var result = await _supportChatService.GetAdminConversationsAsync(query);
        return Ok(result);
    }

    [HttpGet("conversations/{customerId:guid}/messages")]
    public async Task<ActionResult<SupportConversationMessagesDto>> GetConversationMessages(Guid customerId, [FromQuery] SupportMessageQuery query)
    {
        try
        {
            var result = await _supportChatService.GetConversationMessagesForAdminAsync(customerId, query);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("conversations/{customerId:guid}/messages")]
    public async Task<ActionResult<SupportMessageDto>> SendMessage(Guid customerId, [FromBody] SendSupportMessageRequest request)
    {
        try
        {
            var adminId = GetUserId();
            var role = GetUserRole();
            var message = await _supportChatService.SendMessageAsync(adminId, role, customerId, request.Content);
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

    [HttpPost("conversations/{customerId:guid}/read")]
    public async Task<ActionResult> MarkConversationRead(Guid customerId)
    {
        var adminId = GetUserId();
        var role = GetUserRole();
        await _supportChatService.MarkConversationReadAsync(adminId, role, customerId);
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
