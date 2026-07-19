using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PmesCSharp.Services;

public class PayMongoService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _sp;

    public PayMongoService(HttpClient http, IConfiguration config, IServiceProvider sp)
    {
        _http = http;
        _config = config;
        _sp = sp;
    }

    private async Task<AuthenticationHeaderValue> GetAuthAsync(CancellationToken ct)
    {
        var secretKey = _config["PayMongo:SecretKey"] ?? "";

        // If DB settings exist, prefer them (optional)
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PmesCSharp.Data.AppDbContext>();
            var row = await db.SubscriptionGlobalSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
            if (row is not null && !string.IsNullOrWhiteSpace(row.PayMongoSecretKey))
                secretKey = row.PayMongoSecretKey.Trim();
        }
        catch
        {
            // Ignore and fall back to appsettings
        }

        if (string.IsNullOrWhiteSpace(secretKey))
            throw new Exception("PayMongo secret key is not configured.");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(secretKey + ":"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<PayMongoLinkResult> CreatePaymentLinkAsync(string description, long amountCentavos, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    amount = amountCentavos,
                    description,
                    remarks = description,
                    redirect = new { success = successUrl, failed = cancelUrl }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paymongo.com/v1/links")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = await GetAuthAsync(ct);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"PayMongo error: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var attrs = root.GetProperty("data").GetProperty("attributes");

        return new PayMongoLinkResult
        {
            LinkId = root.GetProperty("data").GetProperty("id").GetString() ?? "",
            CheckoutUrl = attrs.GetProperty("checkout_url").GetString() ?? "",
            ReferenceNumber = attrs.GetProperty("reference_number").GetString() ?? "",
        };
    }

    public async Task<bool> IsLinkPaidAsync(string linkId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paymongo.com/v1/links/{linkId}");
        request.Headers.Authorization = await GetAuthAsync(ct);

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("status")
            .GetString();

        return status == "paid";
    }
}

public class PayMongoLinkResult
{
    public string LinkId { get; set; } = "";
    public string CheckoutUrl { get; set; } = "";
    public string ReferenceNumber { get; set; } = "";
}
