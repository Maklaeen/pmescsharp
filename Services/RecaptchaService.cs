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

            using var resp = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content, cancellationToken);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Turnstile verification failed.");
            return true;
        }
    }
}
