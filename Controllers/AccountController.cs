using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Account;
using System.Security.Claims;
using PmesCSharp.Services;

namespace PmesCSharp.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IRecaptchaService _recaptchaService;
    private readonly AppDbContext _db;
    private readonly PmesCSharp.Services.EmailService _email;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IRecaptchaService recaptchaService,
        AppDbContext db,
        PmesCSharp.Services.EmailService email)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _configuration = configuration;
        _recaptchaService = recaptchaService;
        _db = db;
        _email = email;
    }

    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost("/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var recaptchaToken = Request.Form["cf-turnstile-response"].ToString();
        var recaptchaValid = await _recaptchaService.VerifyAsync(recaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!recaptchaValid)
        {
            ModelState.AddModelError(string.Empty, "Verification failed. Please try again.");
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
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost("/register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var recaptchaToken = Request.Form["cf-turnstile-response"].ToString();
        var recaptchaValid = await _recaptchaService.VerifyAsync(recaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!recaptchaValid)
        {
            ModelState.AddModelError(string.Empty, "Verification failed. Please try again.");
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

            await _signInManager.SignInAsync(user, isPersistent: false);

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
                var confirmUrl = Url.Action("ConfirmEmail", "Profile", new { userId = user.Id, token = encoded }, Request.Scheme);

                await _email.SendAsync(
                    user.Email!,
                    "Verify your PMES email",
                    $"""<p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName ?? user.Email)},</p><p>Please verify your email by clicking the link below:</p><p><a href=\"{confirmUrl}\">Verify Email</a></p><p>If you did not create this account, you can ignore this email.</p>"""
                );
            }
            catch
            {
                // Don't block registration if email sending fails.
            }

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
            var encodedToken = Uri.EscapeDataString(token);
            var encodedEmail = Uri.EscapeDataString(model.Email);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var resetUrl = $"{baseUrl}/reset-password?token={encodedToken}&email={encodedEmail}";

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
                              <p style="margin:0 0 28px;font-size:14px;color:#71717a;line-height:1.6;">Hi, this is the PMES team. We received a request to reset the password for your account. Click the button below to set a new password.</p>
                              <table cellpadding="0" cellspacing="0" style="margin:0 auto 28px;">
                                <tr><td style="background:#ea580c;border-radius:10px;">
                                  <a href="{resetUrl}" style="display:inline-block;padding:14px 32px;font-size:14px;font-weight:700;color:#ffffff;text-decoration:none;letter-spacing:0.2px;">Reset Password</a>
                                </td></tr>
                              </table>
                              <p style="margin:0 0 8px;font-size:13px;color:#a1a1aa;">This link expires in <strong>24 hours</strong>. If you did not request a password reset, you can safely ignore this email.</p>
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
                // fallback for dev
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
        if (string.IsNullOrWhiteSpace(token))
            return Redirect("/forgot-password");

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
        if (roles.Contains("planner")) return "/planner";
        if (roles.Contains("inventory")) return "/inventory";
        if (roles.Contains("operator")) return "/operator";
        if (roles.Contains("qc")) return "/qc";
        return "/admin";
    }
}
