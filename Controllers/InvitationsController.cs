using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class InvitationsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailService _email;

    private static readonly string[] AllRoles = ["admin", "planner", "inventory", "operator", "qc"];

    public InvitationsController(AppDbContext db, UserManager<ApplicationUser> userManager, EmailService email)
    {
        _db = db;
        _userManager = userManager;
        _email = email;
    }

    [HttpGet("/admin/invitations")]
    public async Task<IActionResult> Index()
    {
        var invitations = await _db.Invitations
            .Include(i => i.InvitedByUser)
            .OrderByDescending(i => i.Id)
            .ToListAsync();

        ViewBag.Roles = AllRoles;
        return View(invitations);
    }

    [HttpPost("/admin/invitations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send([FromForm] string email, [FromForm] string role)
    {
        // Check if already invited or existing user
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            TempData["Error"] = "A user with this email already exists.";
            return Redirect("/admin/invitations");
        }

        var existingInvite = await _db.Invitations
            .AnyAsync(i => i.Email == email && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow);
        if (existingInvite)
        {
            TempData["Error"] = "An active invitation already exists for this email.";
            return Redirect("/admin/invitations");
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var token = Guid.NewGuid().ToString("N");

        var invitation = new Invitation
        {
            Email = email,
            Role = role,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(3),
            InvitedByUserId = currentUser?.Id,
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync();

        var acceptUrl = $"{Request.Scheme}://{Request.Host}/join/{token}";

        try
        {
            await _email.SendAsync(
                email,
                "You're invited to PMES",
                $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto">
                    <h2 style="color:#f97316">You've been invited to PMES</h2>
                    <p>You have been invited to join the <strong>Production & Manufacturing Execution System</strong> as <strong>{role}</strong>.</p>
                    <p>Click the button below to accept your invitation and set up your account:</p>
                    <a href="{acceptUrl}" style="display:inline-block;background:#f97316;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold;margin:16px 0">Accept Invitation</a>
                    <p style="color:#888;font-size:12px">This invitation expires in 3 days. If you did not expect this, ignore this email.</p>
                </div>
                """
            );
            TempData["Success"] = $"Invitation sent to {email}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Invitation created but email failed: {ex.Message}";
        }

        return Redirect("/admin/invitations");
    }

    [HttpPost("/admin/invitations/{id:int}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id)
    {
        var invite = await _db.Invitations.FindAsync(id);
        if (invite is not null)
        {
            _db.Invitations.Remove(invite);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Invitation revoked.";
        return Redirect("/admin/invitations");
    }
}
