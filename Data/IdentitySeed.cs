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

        // Enforce single superadmin: remove the role from anyone else.
        var existingSuperAdmins = await userManager.GetUsersInRoleAsync("superadmin");
        foreach (var u in existingSuperAdmins)
        {
            if (!string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                await userManager.RemoveFromRoleAsync(u, "superadmin");
                // Keep them as admin (default) so they still have access.
                if (!await userManager.IsInRoleAsync(u, "admin"))
                    await userManager.AddToRoleAsync(u, "admin");
            }
        }

        // Remove legacy superadmin if exists
        var legacy = await userManager.FindByEmailAsync("admin@pmes.com");
        if (legacy is not null)
        {
            var legacyRoles = await userManager.GetRolesAsync(legacy);
            await userManager.RemoveFromRolesAsync(legacy, legacyRoles);
            await userManager.DeleteAsync(legacy);
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
        else
        {
            if (!await userManager.IsInRoleAsync(existing, "superadmin"))
                await userManager.AddToRoleAsync(existing, "superadmin");
        }
    }
}
