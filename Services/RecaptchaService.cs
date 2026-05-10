using System.Text.Json;

namespace PmesCSharp.Services;

public interface IRecaptchaService
{
    Task<bool> VerifyAsync(string? responseToken, string? remoteIp, CancellationToken cancellationToken);
}

public sealed class RecaptchaService : IRecaptchaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecaptchaService> _logger;

    public RecaptchaService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<RecaptchaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? responseToken, string? remoteIp, CancellationToken cancellationToken)
    {
        // If not configured, don't block auth flows.
        var secret = _configuration["Recaptcha:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret)) return true;

        if (string.IsNullOrWhiteSpace(responseToken)) return false;

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = secret,
                ["response"] = responseToken,
                ["remoteip"] = remoteIp ?? string.Empty,
            });

            using var resp = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content, cancellationToken);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successProp)) return false;
            var success = successProp.GetBoolean();

            if (!success) return false;

            // Optional: score (v3) if present.
            if (root.TryGetProperty("score", out var scoreProp))
            {
                var minScore = 0.5;
                var configured = _configuration["Recaptcha:MinScore"];
                if (!string.IsNullOrWhiteSpace(configured) && double.TryParse(configured, out var parsed))
                    minScore = parsed;

                var score = scoreProp.GetDouble();
                if (score < minScore) return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "reCAPTCHA verification failed.");
            // Fail open on infra errors to avoid locking out logins.
            return true;
        }
    }
}
