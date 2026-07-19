using System.Security.Claims;

namespace PmesCSharp.Data;

public sealed class CurrentCompany : ICurrentCompany
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentCompany(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int CompanyId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            // 0 means "no tenant filtering" (intended for superadmin or non-request contexts).
            // -1 means "deny/unknown tenant" (secure default for requests without a resolved company).
            if (httpContext is null)
                return 0;

            if (user?.Identity?.IsAuthenticated != true)
                return 0;

            if (user.IsInRole("superadmin"))
                return 0;

            var value = user.FindFirstValue("CompanyId");
            if (string.IsNullOrWhiteSpace(value))
                return -1;

            return int.TryParse(value, out var id) && id > 0 ? id : -1;
        }
    }
}
