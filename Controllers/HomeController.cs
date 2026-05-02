using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();

    [HttpGet("/privacy")]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet("/offline")]
    [AllowAnonymous]
    public IActionResult Offline() => View();
}
