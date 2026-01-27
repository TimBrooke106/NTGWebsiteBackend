using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkipHire.Api.Models;

namespace SkipHire.Api.Controllers;

public class AdminAuthSettings
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private static readonly HashSet<string> Tokens = new(); // in-memory session tokens
    private readonly AdminAuthSettings _auth;

    public AdminController(IOptions<AdminAuthSettings> auth)
    {
        _auth = auth.Value;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] AdminLoginRequest req)
    {
        if (req.Username == _auth.Username && req.Password == _auth.Password)
        {
            var token = Guid.NewGuid().ToString("N");
            Tokens.Add(token);
            return Ok(new { token });
        }

        return Unauthorized(new { message = "Invalid credentials" });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var token = Request.Headers["X-Admin-Token"].ToString();
        if (!string.IsNullOrWhiteSpace(token))
            Tokens.Remove(token);

        return Ok();
    }

    // Helper for other controllers
    public static bool IsValidToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && Tokens.Contains(token);
}
