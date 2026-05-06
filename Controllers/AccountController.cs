using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Account;

namespace PmesCSharp.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        AppDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _configuration = configuration;
       _db = db;
    }

    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost("/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "These credentials do not match our records.");
            return View(model);
        }

        if (!user.IsApproved)
        {
            ModelState.AddModelError(string.Empty, "Your account is pending admin approval.");
            return View(model);
        }

     var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "These credentials do not match our records.");
            return View(model);
        }

        // Re-issue auth cookie so CompanyId claim is present
        await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);

        var roles = await _userManager.GetRolesAsync(user);
        return Redirect(ResolveDashboardPath(roles));
    }

    [HttpGet("/register")]
    [AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost("/register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "The email has already been taken.");
            return View(model);
        }

      // Create company + admin user in one transaction
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var codeBase = SlugifyCompanyCode(model.CompanyName);
            var code = await EnsureUniqueCompanyCodeAsync(codeBase, cancellationToken);

            var company = new Company
            {
                Name = model.CompanyName.Trim(),
                Code = code,
            };

            _db.Companies.Add(company);
            await _db.SaveChangesAsync(cancellationToken);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.Name,
                EmailConfirmed = true,
                CompanyId = company.Id,
              IsApproved = true,
                ApprovedAt = DateTime.UtcNow,
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "admin");
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            await tx.CommitAsync(cancellationToken);

            await _signInManager.SignInAsync(user, isPersistent: false);
            return Redirect("/subscription/setup");
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(cancellationToken);
            ModelState.AddModelError(nameof(model.CompanyName), "Company name/code already exists. Please try a different name.");
            return View(model);
        }
    }

    private static string SlugifyCompanyCode(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "company";
        var sb = new System.Text.StringBuilder();
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_') sb.Append('-');
        }

        var code = sb.ToString();
        while (code.Contains("--", StringComparison.Ordinal))
            code = code.Replace("--", "-", StringComparison.Ordinal);

        code = code.Trim('-');
        return string.IsNullOrWhiteSpace(code) ? "company" : code;
    }

    private async Task<string> EnsureUniqueCompanyCodeAsync(string codeBase, CancellationToken cancellationToken)
    {
        var code = codeBase;
        var n = 1;
        while (await _db.Companies.AnyAsync(c => c.Code == code, cancellationToken))
        {
            n++;
            code = $"{codeBase}-{n}";
        }
        return code;
    }

    [HttpGet("/forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("/forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        // Always show a success-like status (avoid account enumeration)
        var status = "If your email exists in our system, you will receive a password reset link.";

        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = Url.Action("ResetPassword", "Account", new { token }, Request.Scheme);
            if (resetUrl is not null)
            {
                // For school/dev: surface the link without requiring SMTP.
                if (_configuration.GetValue<bool>("Seed:Enabled"))
                {
                    status = $"Reset link (dev): {resetUrl}?email={Uri.EscapeDataString(model.Email)}";
                }
            }
        }

        TempData["Status"] = status;
        return RedirectToAction(nameof(ForgotPassword));
    }

    [HttpGet("/reset-password/{token}")]
    [AllowAnonymous]
    public IActionResult ResetPassword(string token, [FromQuery] string? email)
    {
        var vm = new ResetPasswordViewModel
        {
            Token = token,
            Email = email ?? string.Empty
        };
        return View(vm);
    }

    [HttpPost("/reset-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            TempData["Status"] = "Your password has been reset.";
            return Redirect("/login");
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["Status"] = "Your password has been reset.";
        return Redirect("/login");
    }

    [HttpPost("/logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private static string ResolveDashboardPath(IList<string> roles)
    {
        if (roles.Contains("planner")) return "/planner/dashboard";
        if (roles.Contains("inventory")) return "/inventory/dashboard";
        if (roles.Contains("operator")) return "/operator/dashboard";
        if (roles.Contains("qc")) return "/qc/dashboard";
        return "/admin/dashboard";
    }
}
