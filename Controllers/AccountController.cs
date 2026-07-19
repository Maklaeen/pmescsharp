using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Account;
using PmesCSharp.Services;
using System.Security.Claims;

namespace PmesCSharp.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly IRecaptchaService _recaptcha;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        AppDbContext db,
        EmailService email,
        IRecaptchaService recaptcha)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
        _email = email;
        _recaptcha = recaptcha;
    }

    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        ViewData["RecaptchaSiteKey"] =
            _configuration["Recaptcha:SiteKey"] ??
            _configuration["ReCaptchaSettings:SiteKey"];

        return View(new LoginViewModel());
    }

    [HttpPost("/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var captchaToken = Request.Form["g-recaptcha-response"].ToString();
        if (!await _recaptcha.VerifyAsync(captchaToken, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "reCAPTCHA validation failed. Please try again.");
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

        await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);
        var roles = await _userManager.GetRolesAsync(user);
        return Redirect(ResolveDashboardPath(roles));
    }

    [HttpGet("/register")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        ViewData["RecaptchaSiteKey"] =
            _configuration["Recaptcha:SiteKey"] ??
            _configuration["ReCaptchaSettings:SiteKey"];

        return View(new RegisterViewModel());
    }

    [HttpPost("/register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var captchaToken = Request.Form["g-recaptcha-response"].ToString();
        if (!await _recaptcha.VerifyAsync(captchaToken, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "reCAPTCHA validation failed. Please try again.");
            return View(model);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "The email has already been taken.");
            return View(model);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var codeBase = SlugifyCompanyCode(model.CompanyName);
            var code = await EnsureUniqueCompanyCodeAsync(codeBase, cancellationToken);

            var company = new Company { Name = model.CompanyName.Trim(), Code = code };
            _db.Companies.Add(company);
            await _db.SaveChangesAsync(cancellationToken);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.Name,
                EmailConfirmed = false,
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

            // Auto-create Free subscription so admin can use the system immediately
            _db.Add(new CompanySubscription
            {
                CompanyId = company.Id,
                Plan = SubscriptionPlan.Free,
                BillingCycle = SubscriptionBillingCycle.Monthly,
                Status = SubscriptionStatus.Active,
                BillingEmail = model.Email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken);

            await _signInManager.SignInAsync(user, isPersistent: false);

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
                var confirmUrl = Url.Action("ConfirmEmail", "Profile", new { userId = user.Id, token = encoded }, Request.Scheme);
                await _email.SendAsync(
                    user.Email!,
                    "Verify your PMES email",
                    $"""<p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName ?? user.Email)},</p><p>Please verify your email by clicking the link below:</p><p><a href="{confirmUrl}">Verify Email</a></p><p>If you did not create this account, you can ignore this email.</p>"""
                );
            }
            catch { }

            return Redirect("/onboarding/company");
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(cancellationToken);
            ModelState.AddModelError(nameof(model.CompanyName), "Company name/code already exists. Please try a different name.");
            return View(model);
        }
    }

    [HttpGet("/signin-google")]
    [AllowAnonymous]
    public IActionResult SignInWithGoogle()
    {
        // Important: this must NOT be the same path as GoogleOptions.CallbackPath.
        // CallbackPath is handled by the Google auth middleware, which then redirects here.
        var callbackUrl = Url.Action(nameof(GoogleComplete), "Account", null, Request.Scheme)!;
        var props = _signInManager.ConfigureExternalAuthenticationProperties("Google", callbackUrl);
        return Challenge(props, "Google");
    }

    [HttpGet("/signin-google/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleComplete(CancellationToken cancellationToken)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null) { TempData["Error"] = "Google sign-in failed."; return Redirect("/login"); }

        var email = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email)) { TempData["Error"] = "Google sign-in failed (missing email)."; return Redirect("/login"); }

        var user = await _userManager.FindByEmailAsync(email);

        // If no account exists, create a new company + admin account automatically
        if (user is null)
        {
            var fullName = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? email;
            var companyName = $"{fullName.Split(' ').FirstOrDefault() ?? "My"}'s Company";
            var codeBase = SlugifyCompanyCode(companyName);
            var code = await EnsureUniqueCompanyCodeAsync(codeBase, cancellationToken);

            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var company = new Company { Name = companyName.Trim(), Code = code };
                _db.Companies.Add(company);
                await _db.SaveChangesAsync(cancellationToken);

                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true,
                    CompanyId = company.Id,
                    IsApproved = true,
                    ApprovedAt = DateTime.UtcNow,
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    TempData["Error"] = "Failed to create account. Please try registering manually.";
                    return Redirect("/register");
                }

                await _userManager.AddToRoleAsync(user, "admin");
                await _userManager.AddLoginAsync(user, info);

                await tx.CommitAsync(cancellationToken);

                // Auto-create Free subscription
                _db.Add(new CompanySubscription
                {
                    CompanyId = company.Id,
                    Plan = SubscriptionPlan.Free,
                    BillingCycle = SubscriptionBillingCycle.Monthly,
                    Status = SubscriptionStatus.Active,
                    BillingEmail = email,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
                await _db.SaveChangesAsync(cancellationToken);

                await _signInManager.SignInAsync(user, isPersistent: false);
                return Redirect("/onboarding/company");
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                TempData["Error"] = "Failed to create account. Please try registering manually.";
                return Redirect("/register");
            }
        }

        if (!user.IsApproved) { TempData["Error"] = "Your account is pending admin approval."; return Redirect("/login"); }

        var logins = await _userManager.GetLoginsAsync(user);
        if (!logins.Any(l => l.LoginProvider == info.LoginProvider && l.ProviderKey == info.ProviderKey))
            await _userManager.AddLoginAsync(user, info);

        // If user has no roles yet, default to Admin (never SuperAdmin).
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            await _userManager.AddToRoleAsync(user, "admin");
            roles = await _userManager.GetRolesAsync(user);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        var redirectPath = await ResolvePostSignInPathAsync(user, roles, cancellationToken);
        return Redirect(redirectPath);
    }

    private async Task<string> ResolvePostSignInPathAsync(ApplicationUser user, IList<string> roles, CancellationToken cancellationToken)
    {
        // Superadmin goes straight to dashboard
        if (roles.Contains("superadmin")) return "/admin";

        // Admin onboarding: require subscription setup only if no subscription at all
        if (roles.Contains("admin") && user.CompanyId is int companyId && companyId > 0)
        {
            var hasSubscription = await _db.Set<CompanySubscription>()
                .AsNoTracking()
                .AnyAsync(s => s.CompanyId == companyId, cancellationToken);
            if (!hasSubscription) return "/subscription/setup";
        }

        return ResolveDashboardPath(roles);
    }

    [HttpGet("/forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("/forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        var status = "If your email exists in our system, you will receive a password reset link.";

        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Uri.EscapeDataString(token);
            var encodedEmail = Uri.EscapeDataString(model.Email);
            var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password?token={encodedToken}&email={encodedEmail}";

            try
            {
                await _email.SendAsync(
                    model.Email,
                    "Reset your PMES password",
                    $"""
                    <!DOCTYPE html>
                    <html>
                    <body style="margin:0;padding:0;background:#f4f4f5;font-family:Inter,Arial,sans-serif;">
                      <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:40px 0;">
                        <tr><td align="center">
                          <table width="520" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.06);">
                            <tr><td style="background:#18181b;padding:32px 40px;text-align:center;">
                              <span style="font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.5px;">PMES</span>
                            </td></tr>
                            <tr><td style="padding:40px 40px 32px;">
                              <p style="margin:0 0 8px;font-size:20px;font-weight:700;color:#18181b;">Reset your password</p>
                              <p style="margin:0 0 28px;font-size:14px;color:#71717a;line-height:1.6;">Click the button below to set a new password.</p>
                              <table cellpadding="0" cellspacing="0" style="margin:0 auto 28px;">
                                <tr><td style="background:#ea580c;border-radius:10px;">
                                  <a href="{resetUrl}" style="display:inline-block;padding:14px 32px;font-size:14px;font-weight:700;color:#ffffff;text-decoration:none;">Reset Password</a>
                                </td></tr>
                              </table>
                              <p style="margin:0;font-size:13px;color:#a1a1aa;">This link expires in <strong>24 hours</strong>.</p>
                            </td></tr>
                            <tr><td style="background:#f4f4f5;padding:20px 40px;text-align:center;border-top:1px solid #e4e4e7;">
                              <p style="margin:0;font-size:12px;color:#a1a1aa;">&copy; {DateTime.UtcNow.Year} PMES. All rights reserved.</p>
                            </td></tr>
                          </table>
                        </td></tr>
                      </table>
                    </body>
                    </html>
                    """
                );
                status = "Password reset link has been sent to your email.";
            }
            catch
            {
                status = $"Reset link (dev): {resetUrl}";
            }
        }

        TempData["Status"] = status;
        return RedirectToAction(nameof(ForgotPassword));
    }

    [HttpGet("/reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword([FromQuery] string token, [FromQuery] string? email)
    {
        if (string.IsNullOrWhiteSpace(token)) return Redirect("/forgot-password");
        return View(new ResetPasswordViewModel { Token = token, Email = email ?? string.Empty });
    }

    [HttpPost("/reset-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null) { TempData["Status"] = "Your password has been reset."; return Redirect("/login"); }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
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
            code = $"{codeBase}-{++n}";
        return code;
    }

    private static string ResolveDashboardPath(IList<string> roles)
    {
        if (roles.Contains("superadmin")) return "/admin";
        if (roles.Contains("admin")) return "/admin";
        if (roles.Contains("planner")) return "/planner";
        if (roles.Contains("inventory")) return "/inventory";
        if (roles.Contains("operator")) return "/operator";
        if (roles.Contains("qc")) return "/qc";
        return "/admin";
    }
}
