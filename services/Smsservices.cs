using System.Net.Http.Json;

namespace Commute360.Services;

// Helpers for phone numbers. Everything is stored in international format, e.g. +233241234567.
public static class PhoneNumberHelper
{
    // Accepts 0241234567, 241234567, 233241234567, +233241234567 and 00233241234567
    // (and other international numbers that start with + or 00).
    public static bool TryNormalize(string? input, out string e164)
    {
        e164 = "";
        if (string.IsNullOrWhiteSpace(input)) return false;

        var raw = input.Trim();
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return false;

        if (digits.StartsWith("00"))
            digits = digits.Substring(2);                    // 00233... means international
        else if (!raw.StartsWith("+") && digits.StartsWith("0"))
            digits = "233" + digits.Substring(1);            // 024... is a Ghana number
        else if (!raw.StartsWith("+") && digits.Length == 9)
            digits = "233" + digits;                         // 241234567 (no leading 0)

        if (digits.Length < 8 || digits.Length > 15) return false;                 // E.164 limit
        if (digits.StartsWith("233") && digits.Length != 12) return false;         // Ghana: 233 + 9 digits

        e164 = "+" + digits;
        return true;
    }

    // +23324****567, so pages can show which number is saved without exposing it
    public static string Mask(string e164)
    {
        if (string.IsNullOrEmpty(e164) || e164.Length < 9) return "***";
        return e164.Substring(0, 6) + new string('*', e164.Length - 9) + e164.Substring(e164.Length - 3);
    }
}

public interface ISmsSender
{
    // toE164 is an international number such as +233241234567
    Task SendAsync(string toE164, string message);

    // Same message to many people, sent in as few requests as possible
    Task SendManyAsync(IReadOnlyCollection<string> toE164, string message);
}

// Sends SMS through Arkesel's HTTPS API (a Ghana SMS provider).
// Settings (on Render: environment variables, with __ instead of :):
//   Sms:ArkeselApiKey   your Arkesel API key
//   Sms:SenderId        sender name shown on the phone, 11 characters at most (default "Commute360")
//   Sms:Sandbox         "true" to test without delivering or being charged
public class ArkeselSmsSender : ISmsSender
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ArkeselSmsSender> _logger;

    public ArkeselSmsSender(IHttpClientFactory http, IConfiguration config, ILogger<ArkeselSmsSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public Task SendAsync(string toE164, string message) =>
        SendManyAsync(new[] { toE164 }, message);

    public async Task SendManyAsync(IReadOnlyCollection<string> toE164, string message)
    {
        var apiKey = _config["Sms:ArkeselApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("SMS is not configured (Sms:ArkeselApiKey). No SMS sent to {Count} number(s).", toE164.Count);
            return;
        }

        var sender = _config["Sms:SenderId"] ?? "Commute360";
        if (sender.Length > 11) sender = sender.Substring(0, 11);

        // Arkesel wants 233241234567 (no plus). Send in batches of 100.
        var numbers = toE164.Select(n => n.TrimStart('+')).Distinct().ToList();

        foreach (var batch in numbers.Chunk(100))
        {
            try
            {
                var body = new Dictionary<string, object>
                {
                    ["sender"] = sender,
                    ["message"] = message,
                    ["recipients"] = batch
                };
                if (_config.GetValue<bool>("Sms:Sandbox")) body["sandbox"] = true;

                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://sms.arkesel.com/api/v2/sms/send");
                request.Headers.Add("api-key", apiKey);
                request.Content = JsonContent.Create(body);

                using var response = await client.SendAsync(request);
                var reply = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode || !reply.Contains("success", StringComparison.OrdinalIgnoreCase))
                    _logger.LogError("Arkesel did not accept an SMS batch of {Count}: {Status} {Reply}",
                        batch.Length, (int)response.StatusCode, reply);
            }
            catch (Exception ex)
            {
                // Never break a request because an SMS failed
                _logger.LogError(ex, "Failed to send an SMS batch of {Count}", batch.Length);
            }
        }
    }
}

// Used when no SMS provider is configured: the message is only written to the log.
public class LogSmsSender : ISmsSender
{
    private readonly ILogger<LogSmsSender> _logger;

    public LogSmsSender(ILogger<LogSmsSender> logger) => _logger = logger;

    public Task SendAsync(string toE164, string message)
    {
        _logger.LogWarning("SMS is not configured. Would have sent to {Number}: {Message}", PhoneNumberHelper.Mask(toE164), message);
        return Task.CompletedTask;
    }

    public Task SendManyAsync(IReadOnlyCollection<string> toE164, string message)
    {
        _logger.LogWarning("SMS is not configured. Would have sent to {Count} number(s): {Message}", toE164.Count, message);
        return Task.CompletedTask;
    }
}