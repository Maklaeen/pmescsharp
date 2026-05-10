using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PmesCSharp.Models;
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

    public ProfileController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, PmesCSharp.Services.EmailService email)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _email = email;
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

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.Password);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
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
}
