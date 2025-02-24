using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Radio.Server.Services;
using MongoDB.Driver;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Radio.Server.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class UserController(MongoDbService mongoDbService) : ControllerBase {
    
    [Authorize(Policy = "PrivilegedOnly")]
    [HttpGet("me")]
    public async Task<IActionResult> GetUserData() {
        
        // Extract the username from the token claims
        var username = User.FindFirst("name")?.Value;
    
        if (string.IsNullOrEmpty(username)) {
            return Unauthorized("Username is missing from the token.");
        }

        // Retrieve user data from your MongoDB collection by username
        var user = await mongoDbService.GetUserCollection()
            .Find(u => u.Username == username)
            .FirstOrDefaultAsync();
    
        if (user == null) {
            return NotFound("User not found.");
        }

        // Customize the response data as needed
        var response = new {
            name = user.Username,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
        };

        return Ok(response);
    }
    
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout() {
        if (Request.Cookies.ContainsKey("token")) {
            Response.Cookies.Delete("token", new CookieOptions { HttpOnly = true, Secure = true });
        }
        return Ok(new { Message = "Logged out successfully." });
    }
    
    [Authorize]
    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata() {
        const string icecastUrl = "http://192.168.1.20:8000/status-json.xsl";
    
        using var client = new HttpClient();
        try {
            var response = await client.GetStringAsync(icecastUrl);
            var metadata = JsonDocument.Parse(response);
    
            // Extract current track info from Icecast JSON
            var source = metadata.RootElement
                .GetProperty("icestats")
                .GetProperty("source");
        
            return Ok(new {
                title = source.GetProperty("title").GetString(),
                artist = source.GetProperty("artist").GetString()
            });
        }
        catch (HttpRequestException ex) {
            return StatusCode(500, new { Message = "Error fetching metadata", Error = ex.Message });
        }
    }
}