using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Account;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace PmesCSharp.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly PmesCSharp.Services.EmailService _email;
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;

    public ProfileController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, PmesCSharp.Services.EmailService email, AppDbContext db, IAuditLogger audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _email = email;
        _db = db;
        _audit = audit;
    }

    [HttpGet("/profile")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");
        return View(user);
    }

    [HttpGet("/profile/edit")]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");
        ViewBag.UpdateModel = new PmesCSharp.ViewModels.Account.UpdateProfileViewModel
        {
            Name = user.FullName ?? "",
            Email = user.Email ?? "",
            DateOfBirth = user.DateOfBirth,
            MobileNumber = user.MobileNumber,
            Sex = user.Sex,
        };
        return View(user);
    }

    [HttpPost("/profile/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please check your input.";
            return Redirect("/profile");
        }

        user.FullName = model.Name;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.NormalizedEmail = model.Email.ToUpperInvariant();
        user.NormalizedUserName = model.Email.ToUpperInvariant();
        user.DateOfBirth = model.DateOfBirth;
        user.MobileNumber = string.IsNullOrWhiteSpace(model.MobileNumber) ? null : model.MobileNumber.Trim();
        user.Sex = string.IsNullOrWhiteSpace(model.Sex) ? null : model.Sex.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            await _audit.LogAsync("user.profile.update", "User", user.Id, $"Updated profile: Name={user.FullName}; Email={user.Email}; DOB={user.DateOfBirth:yyyy-MM-dd}; Mobile={user.MobileNumber}; Sex={user.Sex}");
            TempData["Success"] = "Profile updated successfully.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }

        return Redirect("/profile");
    }

    [HttpPost("/profile/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please check your input.";
            return Redirect("/profile");
        }

        // Google sign-in users with no password — use AddPasswordAsync instead
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            var addResult = await _userManager.AddPasswordAsync(user, model.Password);
            if (addResult.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                await _audit.LogAsync("user.password.set", "User", user.Id, "Set password for account");
                TempData["Success"] = "Password set successfully. You can now log in with email and password.";
            }
            else
            {
                TempData["Error"] = string.Join(" ", addResult.Errors.Select(e => e.Description));
            }
            return Redirect("/profile");
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.Password);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            await _audit.LogAsync("user.password.change", "User", user.Id, "Changed password for account");
            TempData["Success"] = "Password changed successfully.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }

        return Redirect("/profile");
    }

    [HttpPost("/profile/email/send-verification")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendVerificationEmail()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        if (user.EmailConfirmed)
        {
            TempData["Success"] = "Your email is already verified.";
            return Redirect("/profile");
        }

        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirmUrl = Url.Action("ConfirmEmail", "Profile", new { userId = user.Id, token = encoded }, Request.Scheme);

            await _email.SendAsync(
                user.Email!,
                "Verify your PMES email",
                $"""<p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName ?? user.Email)},</p><p>Please verify your email by clicking the link below:</p><p><a href=\"{confirmUrl}\">Verify Email</a></p>"""
            );

            await _audit.LogAsync("user.email.verification.sent", "User", user.Id, "Sent email verification request");
            TempData["Success"] = "Verification email sent. Please check your inbox.";
        }
        catch
        {
            TempData["Error"] = "Failed to send verification email. Please try again later.";
        }

        return Redirect("/profile");
    }

    [HttpGet("/profile/confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return Redirect("/login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Redirect("/login");

        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await _userManager.ConfirmEmailAsync(user, decoded);
            if (result.Succeeded)
            {
                await _audit.LogAsync("user.email.verified", "User", user.Id, "Verified email address");
            }
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? "Email verified successfully."
                : "Invalid or expired verification link.";
        }
        catch
        {
            TempData["Error"] = "Invalid or expired verification link.";
        }

        return Redirect("/profile");
    }

    [HttpPost("/profile/delete/send-code")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDeleteCode()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "User not found." });

        // Generate 6-digit code and store in session
        var code = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString("delete_account_code", code);
        HttpContext.Session.SetString("delete_account_code_expiry", DateTime.UtcNow.AddMinutes(10).ToString("O"));

        try
        {
            await _email.SendAsync(
                user.Email!,
                "PMES Account Deletion Verification",
                $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto">
                    <h2 style="color:#ef4444">Account Deletion Request</h2>
                    <p>You requested to delete your PMES account. Use the code below to confirm:</p>
                    <div style="font-size:36px;font-weight:900;letter-spacing:0.3em;color:#ef4444;text-align:center;padding:20px;background:#1a1a1a;border-radius:12px;margin:20px 0">{code}</div>
                    <p style="color:#888;font-size:12px">This code expires in 10 minutes. If you did not request this, ignore this email.</p>
                </div>
                """
            );
            await _audit.LogAsync("user.delete_code.sent", "User", user.Id, "Requested account deletion verification code");
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Failed to send email: {ex.Message}" });
        }
    }

    [HttpPost("/profile/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount([FromForm] string code)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        // Verify code
        var storedCode = HttpContext.Session.GetString("delete_account_code");
        var expiryStr = HttpContext.Session.GetString("delete_account_code_expiry");

        if (string.IsNullOrWhiteSpace(storedCode) || storedCode != code?.Trim())
        {
            TempData["Error"] = "Invalid verification code. Please try again.";
            return Redirect("/profile");
        }

        if (DateTime.TryParse(expiryStr, out var expiry) && DateTime.UtcNow > expiry)
        {
            TempData["Error"] = "Verification code has expired. Please request a new one.";
            return Redirect("/profile");
        }

        HttpContext.Session.Remove("delete_account_code");
        HttpContext.Session.Remove("delete_account_code_expiry");

        var isAdmin = await _userManager.IsInRoleAsync(user, "admin");
        var companyId = user.CompanyId;
        var userId = user.Id;

        // Sign out first
        await _signInManager.SignOutAsync();

        // If admin, delete company and all related data BEFORE deleting the user
        if (isAdmin && companyId.HasValue && companyId > 0)
        {
            try
            {
                // Delete all users of this company first (except current user — delete last)
                var companyUsers = await _userManager.Users
                    .Where(u => u.CompanyId == companyId.Value && u.Id != userId)
                    .ToListAsync();
                foreach (var u in companyUsers)
                    await _userManager.DeleteAsync(u);

                // Now delete the admin user
                var adminUser = await _userManager.FindByIdAsync(userId);
                if (adminUser is not null)
                    await _userManager.DeleteAsync(adminUser);

                // Delete the company (delete related data first)
                var company = await _db.Companies.FindAsync(companyId.Value);
                if (company is not null)
                {
                    // Delete subscriptions
                    var subs = _db.CompanySubscriptions.Where(s => s.CompanyId == companyId.Value);
                    _db.CompanySubscriptions.RemoveRange(subs);

                    // Delete company profiles
                    var profiles = _db.CompanyProfiles.Where(p => p.CompanyId == companyId.Value);
                    _db.CompanyProfiles.RemoveRange(profiles);

                    // Delete company invites
                    var invites = _db.Set<PmesCSharp.Models.CompanyInvite>().Where(i => i.CompanyId == companyId.Value);
                    _db.Set<PmesCSharp.Models.CompanyInvite>().RemoveRange(invites);

                    await _db.SaveChangesAsync();

                    _db.Companies.Remove(company);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash
                _ = ex.Message;
            }
        }
        else
        {
            // Non-admin: just delete the user
            var userToDelete = await _userManager.FindByIdAsync(userId);
            if (userToDelete is not null)
                await _userManager.DeleteAsync(userToDelete);
        }

        await _audit.LogAsync("user.account.delete", "User", userId, $"Deleted account for {user.Email}; admin={isAdmin}; companyId={companyId}");
        TempData["Success"] = "Your account has been deleted.";
        return Redirect("/");
    }
}