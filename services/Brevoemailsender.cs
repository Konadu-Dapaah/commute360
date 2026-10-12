using System.Net.Http.Json;

namespace Commute360.Services;

// Sends email through Brevo's HTTPS API (port 443).
// Render's free tier blocks the normal SMTP ports (25, 465, 587), so SMTP can't be used there.
//
// Needs these settings (on Render: environment variables, with __ instead of :):
//   Email:BrevoApiKey   an API key from Brevo (SMTP & API > API Keys)
//   Email:From          a sender address you have verified in Brevo
//   Email:FromName      optional display name, defaults to "Commute360"
public class BrevoEmailSender : IEmailSender
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(IHttpClientFactory http, IConfiguration config, ILogger<BrevoEmailSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        var apiKey = _config["Email:BrevoApiKey"];
        var fromEmail = _config["Email:From"];
        var fromName = _config["Email:FromName"] ?? "Commute360";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogWarning("Brevo is not fully configured (Email:BrevoApiKey and Email:From). No email sent to {To}.", to);
            return;
        }

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", apiKey);
            request.Headers.Add("accept", "application/json");
            request.Content = JsonContent.Create(new
            {
                sender = new { name = fromName, email = fromEmail },
                to = new[] { new { email = to } },
                subject,
                textContent = body
            });

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var reason = await response.Content.ReadAsStringAsync();
                _logger.LogError("Brevo rejected the email to {To}: {Status} {Reason}", to, (int)response.StatusCode, reason);
            }
        }
        catch (Exception ex)
        {
            // Never break a request because an email failed
            _logger.LogError(ex, "Failed to send email to {To} through Brevo", to);
        }
    }
}