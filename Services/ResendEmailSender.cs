using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SkipHire.Api.Services;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var apiKey = _config["RESEND_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("RESEND_API_KEY is not set");
            return;
        }

        var from = "NTG Excavations <no-reply@ntgexcavations.com>";

        var payload = new
        {
            from,
            to = new[] { toEmail },
            subject,
            html = htmlBody
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Resend failed: {Status} {Body}", (int)res.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Resend sent OK to {ToEmail}", toEmail);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Resend request cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend HTTP exception");
        }
    }

}
