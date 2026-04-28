using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PmesCSharp.Models;
using PmesCSharp.ViewModels.Account;

namespace PmesCSharp.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ProfileController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet("/profile")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");
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
}
