using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Radio.Server.Models;
using Radio.Server.Services;

namespace Radio.Server.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class ChatController(IHubContext<ChatHub> hubContext) : ControllerBase {
    [Route("send")]
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] ChatMessage message) {
        if (string.IsNullOrEmpty(message.Content))
        {
            return BadRequest("Message cannot be empty.");
        }
    
        // Send message to SignalR clients
        await hubContext.Clients.All.SendAsync("ReceiveMessage", "You", message.Content);
    
        return Ok();
    }
}