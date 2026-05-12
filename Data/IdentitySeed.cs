using Microsoft.AspNetCore.Identity;
using PmesCSharp.Models;

namespace PmesCSharp.Data;

public static class IdentitySeed
{
    public static async Task EnsureSeededAsync(IServiceProvider services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("Seed:Enabled");
        if (!enabled)
        {
            return;
        }

        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "superadmin", "admin", "planner", "inventory", "operator", "qc" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var email = configuration["Seed:SuperAdminEmail"];
        var password = configuration["Seed:SuperAdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Mark Glean Lubiano",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "superadmin");
            }
        }
    }
}
