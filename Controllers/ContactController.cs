using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkipHire.Api.Services;
using SkipHire.Api.Models;

namespace SkipHire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly IEmailSender _emailSender;
    private readonly EmailSettings _emailSettings;

    public ContactController(IEmailSender emailSender, IOptions<EmailSettings> emailSettings)
    {
        _emailSender = emailSender;
        _emailSettings = emailSettings.Value;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ContactMessageRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Subject) ||
            string.IsNullOrWhiteSpace(req.Message))
        {
            return BadRequest(new { message = "Missing required fields." });
        }

        var safeMessage = System.Net.WebUtility.HtmlEncode(req.Message).Replace("\n", "<br/>");
        var safePhone = System.Net.WebUtility.HtmlEncode(req.Phone ?? "");

        var adminSubject = $"CONTACT FORM: {req.Subject}";
        var adminHtml = $@"
            <h2>New Contact Message</h2>
            <p><b>Name:</b> {System.Net.WebUtility.HtmlEncode(req.Name)}</p>
            <p><b>Email:</b> {System.Net.WebUtility.HtmlEncode(req.Email)}</p>
            <p><b>Phone:</b> {safePhone}</p>
            <hr/>
            <p>{safeMessage}</p>
        ";

        // Send to admin
        await _emailSender.SendAsync(_emailSettings.AdminEmail, adminSubject, adminHtml, ct);

        return Ok(new { message = "Message sent." });
    }
}
