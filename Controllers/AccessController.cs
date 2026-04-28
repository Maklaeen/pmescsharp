using Microsoft.AspNetCore.Mvc;

namespace PmesCSharp.Controllers;

public class AccessController : Controller
{
    [HttpGet("/access-denied")]
    public IActionResult Denied() => View();
}
