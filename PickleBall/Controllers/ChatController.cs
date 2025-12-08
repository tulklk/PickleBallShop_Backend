using Microsoft.AspNetCore.Mvc;
using PickleBall.Application.DTOs.Chat;
using PickleBall.Application.Interfaces;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IGeminiChatService _geminiChatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IGeminiChatService geminiChatService,
        ILogger<ChatController> logger)
    {
        _geminiChatService = geminiChatService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the AI chatbot
    /// </summary>
    /// <param name="request">Chat request with message and optional conversation history</param>
    /// <returns>AI chatbot response</returns>
    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Message is required" });
            }

            var response = await _geminiChatService.SendMessageAsync(request);
            
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new ChatResponse
            {
                Success = false,
                Error = "An error occurred while processing your message"
            });
        }
    }

    /// <summary>
    /// Health check for chat service
    /// </summary>
    [HttpGet("health")]
    public ActionResult HealthCheck()
    {
        return Ok(new { status = "ok", message = "Chat service is available" });
    }

    /// <summary>
    /// Test endpoint to verify Gemini API configuration
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult> TestConfiguration()
    {
        try
        {
            var testRequest = new ChatRequest
            {
                Message = "Xin chào"
            };
            
            var response = await _geminiChatService.SendMessageAsync(testRequest);
            
            if (response.Success)
            {
                return Ok(new { 
                    status = "success", 
                    message = "Gemini API is working correctly",
                    response = response.Message 
                });
            }
            
            return BadRequest(new { 
                status = "error", 
                message = "Gemini API test failed",
                error = response.Error 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Gemini API configuration");
            return StatusCode(500, new { 
                status = "error", 
                message = "Configuration test failed",
                error = ex.Message 
            });
        }
    }
}

