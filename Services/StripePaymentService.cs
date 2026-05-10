using Stripe;
using Stripe.Checkout;

namespace PmesCSharp.Services;

public interface IPaymentService
{
    Task<string> CreateCheckoutSessionUrlAsync(int companyId, string plan, bool yearly, string customerEmail, string successUrl, string cancelUrl, CancellationToken cancellationToken);
}

public sealed class StripePaymentService : IPaymentService
{
    private readonly IConfiguration _configuration;

    public StripePaymentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> CreateCheckoutSessionUrlAsync(int companyId, string plan, bool yearly, string customerEmail, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        // If not configured, fall back to a fake success URL (testing-only).
        var apiKey = _configuration["Payments:Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var url = $"{successUrl}{(successUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?')}provider=fake&plan={Uri.EscapeDataString(plan)}&yearly={yearly.ToString().ToLowerInvariant()}";
            return Task.FromResult(url);
        }

        StripeConfiguration.ApiKey = apiKey;

        // Use Stripe test mode keys and a simple one-time payment.
        // Prices are hard-coded for now (you can move to Stripe Price IDs later).
        var amount = (plan.ToLowerInvariant(), yearly) switch
        {
            ("standard", false) => 9900L,  // 99.00
            ("standard", true) => 19400L,  // 194.00
            ("pro", false) => 18900L,      // 189.00
            ("pro", true) => 37800L,       // 378.00
            _ => 9900L
        };

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
            SuccessUrl = $"{successUrl}{(successUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?')}session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = cancelUrl,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _configuration["Payments:Currency"] ?? "usd",
                        UnitAmount = amount,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = yearly ? $"PMES {plan} (Yearly)" : $"PMES {plan} (Monthly)",
                            Description = $"CompanyId={companyId}",
                        }
                    }
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["companyId"] = companyId.ToString(),
                ["plan"] = plan,
                ["yearly"] = yearly.ToString(),
            }
        };

        var service = new SessionService();
        var session = service.Create(options);
        return Task.FromResult(session.Url);
    }
}
