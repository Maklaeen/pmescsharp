using Microsoft.AspNetCore.Identity;

namespace PmesCSharp.Models;

public class ApplicationUser : IdentityUser
{
	public string FullName { get; set; } = "";
}
