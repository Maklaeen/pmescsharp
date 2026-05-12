using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("/notifications")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        var notifications = await _db.Notifications
            .Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        // Mark all as read
        var unread = notifications.Where(n => !n.IsRead).ToList();
        unread.ForEach(n => n.IsRead = true);
        if (unread.Count > 0) await _db.SaveChangesAsync();

        return View(notifications);
    }

    [HttpGet("/notifications/unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Ok(new { count = 0 });

        var count = await _db.Notifications.CountAsync(n => n.UserId == user.Id && !n.IsRead);
        return Ok(new { count });
    }

    [HttpPost("/notifications/{id:int}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user!.Id);
        if (n is not null) { n.IsRead = true; await _db.SaveChangesAsync(); }
        return Ok();
    }

    [HttpPost("/notifications/read-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Redirect("/login");

        var unread = await _db.Notifications.Where(n => n.UserId == user.Id && !n.IsRead).ToListAsync();
        unread.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync();
        TempData["Success"] = "All notifications marked as read.";
        return Redirect("/notifications");
    }
}
