using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PmesCSharp.Services;

public class PayMongoService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public PayMongoService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<PayMongoLinkResult> CreatePaymentLinkAsync(string description, long amountCentavos, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var secretKey = _config["PayMongo:SecretKey"] ?? "";
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(secretKey + ":"));

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("https://api.paymongo.com/v1/links", content, ct);
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
        var secretKey = _config["PayMongo:SecretKey"] ?? "";
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(secretKey + ":"));

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _http.GetAsync($"https://api.paymongo.com/v1/links/{linkId}", ct);
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
