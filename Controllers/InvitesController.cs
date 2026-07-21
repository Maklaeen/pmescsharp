using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;
using PmesCSharp.Services;
using PmesCSharp.ViewModels.Invites;

namespace PmesCSharp.Controllers;

[Authorize(Roles = "superadmin,admin")]
public class InvitesController : Controller
{
    private static readonly string[] InviteRoles = ["operator", "inventory", "qc", "planner", "admin"];

    private readonly AppDbContext _db;
    private readonly ICurrentCompany _currentCompany;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _email;
    private readonly IAuditLogger _audit;

    public InvitesController(AppDbContext db, ICurrentCompany currentCompany, UserManager<ApplicationUser> userManager, IEmailSender email, IAuditLogger audit)
    {
        _db = db;
        _currentCompany = currentCompany;
        _userManager = userManager;
        _email = email;
        _audit = audit;
    }

    [HttpGet("/admin/users/invites")]
    [HttpGet("/users/invites")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (!User.IsInRole("superadmin") && companyId <= 0) return Forbid();

        var now = DateTime.UtcNow;
        var invites = await _db.Set<CompanyInvite>()
            .AsNoTracking()
            .Where(i => User.IsInRole("superadmin") || i.CompanyId == companyId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .Select(i => new InviteListItemViewModel
            {
                Id = i.Id,
                Email = i.InvitedEmail,
                Role = i.Role,
                Code = i.Code,
                ExpiresAt = i.ExpiresAt,
                UsesCount = i.UsesCount,
                MaxUses = i.MaxUses,
                IsActive = i.RevokedAt == null && i.ExpiresAt > now && i.UsesCount < i.MaxUses
            })
            .ToListAsync(cancellationToken);

        ViewBag.Roles = InviteRoles;
        return View(invites);
    }

    [HttpPost("/admin/users/invites")]
    [HttpPost("/users/invites")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InviteCreateViewModel model, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (!User.IsInRole("superadmin") && companyId <= 0) return Forbid();

        if (!InviteRoles.Contains(model.Role))
        {
            TempData["Error"] = "Invalid role.";
            return Redirect("/users/invites");
        }

        var token = GenerateToken();
        var tokenHash = HashToken(token);
        var code = GenerateShortCode();

        var actor = await _userManager.GetUserAsync(User);
        var invite = new CompanyInvite
        {
            CompanyId = companyId,
            InvitedEmail = model.Email.Trim(),
            Role = model.Role,
            TokenHash = tokenHash,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddDays(model.ExpiresInDays),
            MaxUses = 1,
            UsesCount = 0,
            CreatedByUserId = actor?.Id,
        };

        _db.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        var joinUrl = $"{Request.Scheme}://{Request.Host}/join/{token}";
        try
        {
            await _email.SendAsync(invite.InvitedEmail, "PMES: Company invitation",
                $"You have been invited to join a PMES company.\n\nRole: {invite.Role}\nInvite link: {joinUrl}\nInvite code: {invite.Code}\n\nThis invite expires on: {invite.ExpiresAt:yyyy-MM-dd HH:mm} (UTC).\n\nIf you did not expect this invite, you can ignore this email.",
                cancellationToken);
        }
        catch
        {
            TempData["Error"] = "Failed to send invite email. Please check SMTP settings.";
            return Redirect("/users/invites");
        }

        await _audit.LogAsync("invite.create", "CompanyInvite", invite.Id.ToString(), $"Invited {invite.InvitedEmail} as {invite.Role} (exp {invite.ExpiresAt:O})", cancellationToken);
        TempData["Success"] = "Invite sent.";
        return Redirect("/users/invites");
    }

    [HttpPost("/admin/users/invites/quick-code")]
    [HttpPost("/users/invites/quick-code")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickCode([FromForm] string role, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (!User.IsInRole("superadmin") && companyId <= 0) return Forbid();

        if (!InviteRoles.Contains(role))
        {
            TempData["Error"] = "Invalid role.";
            return Redirect("/users/invites");
        }

        var actor = await _userManager.GetUserAsync(User);
        var code = GenerateShortCode();

        var invite = new CompanyInvite
        {
            CompanyId = companyId,
            InvitedEmail = "",
            Role = role,
            TokenHash = HashToken(GenerateToken()),
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            MaxUses = 1,
            UsesCount = 0,
            CreatedByUserId = actor?.Id,
        };

        _db.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("invite.quickcode", "CompanyInvite", invite.Id.ToString(), $"Generated quick code {code} as {role}", cancellationToken);

        TempData["NewCode"] = invite.Code;
        TempData["NewRole"] = invite.Role;
        return Redirect("/users/invites");
    }

    [HttpPost("/admin/users/invites/{id:int}/revoke")]
    [HttpPost("/users/invites/{id:int}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id, CancellationToken cancellationToken)
    {
        var companyId = _currentCompany.CompanyId;
        if (!User.IsInRole("superadmin") && companyId <= 0) return Forbid();

        var invite = await _db.Set<CompanyInvite>().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invite is null) return NotFound();
        if (!User.IsInRole("superadmin") && invite.CompanyId != companyId) return NotFound();

        invite.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("invite.revoke", "CompanyInvite", invite.Id.ToString(), $"Revoked invite for {invite.InvitedEmail}", cancellationToken);
        TempData["Success"] = "Invite revoked.";
        return Redirect("/users/invites");
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateShortCode()
    {
        // Short human-friendly code (no ambiguous chars)
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[8];
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
