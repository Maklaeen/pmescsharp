using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.Models;

/// <summary>
/// Represents a one-time password (OTP) code sent to a user's email for login verification.
/// </summary>
public class OtpCode
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(255)]
    public string UserId { get; set; } = "";

    public ApplicationUser? User { get; set; }

    [Required]
    [StringLength(6)]
    public string Code { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// OTP codes expire after 10 minutes by default.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// Number of failed verification attempts.
    /// </summary>
    public int FailedAttempts { get; set; } = 0;
}
