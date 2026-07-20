using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PmesCSharp.Models;

namespace PmesCSharp.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();

    [HttpGet("/privacy")]
    public IActionResult Privacy() => View();

    [HttpGet("/error")]
    [HttpPost("/error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        var ex = exceptionFeature?.Error;

        var vm = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };

        if (ex is not null)
        {
            var msg = ex.ToString();
            if (ex is Microsoft.Data.SqlClient.SqlException || msg.Contains("SqlException") || msg.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                vm.ErrorType = "db";
                vm.Title = "Database Connection Error";
                vm.Message = "Unable to connect to the database. Please try again in a moment.";
            }
            else if (ex is TimeoutException || ex is OperationCanceledException || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                vm.ErrorType = "timeout";
                vm.Title = "Request Timed Out";
                vm.Message = "The server took too long to respond. Please try again.";
            }
            else
            {
                vm.ErrorType = "unexpected";
                vm.Title = "Something Went Wrong";
                vm.Message = "An unexpected error occurred. Please try again or contact support.";
            }
        }
        else
        {
            vm.ErrorType = "unexpected";
            vm.Title = "Something Went Wrong";
            vm.Message = "An unexpected error occurred. Please try again or contact support.";
        }

        return View(vm);
    }

    [HttpGet("/error/{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCodeError(int statusCode)
    {
        var vm = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };

        return statusCode switch
        {
            404 => View("NotFound"),
            401 => View("SessionExpired"),
            403 => View("Denied"),
            408 => View("Error", new ErrorViewModel { RequestId = vm.RequestId, ErrorType = "timeout", Title = "Request Timed Out", Message = "The server took too long to respond. Please try again." }),
            503 or 502 => View("Error", new ErrorViewModel { RequestId = vm.RequestId, ErrorType = "offline", Title = "Service Unavailable", Message = "The service is temporarily unavailable. Please try again shortly." }),
            504 => View("Error", new ErrorViewModel { RequestId = vm.RequestId, ErrorType = "timeout", Title = "Gateway Timed Out", Message = "The server did not respond in time. Please try again." }),
            _ => View("Error", new ErrorViewModel { RequestId = vm.RequestId, ErrorType = "unexpected", Title = "Something Went Wrong", Message = $"An error occurred (HTTP {statusCode}). Please try again." })
        };
    }

    [HttpGet("/offline")]
    [AllowAnonymous]
    public IActionResult Offline() => View();
}
