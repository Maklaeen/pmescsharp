namespace PmesCSharp.Services;

/// <summary>
/// OTP (One-Time Password) service for login verification.
/// Generates 6-digit codes that expire after 10 minutes.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generate and send OTP code to user email.
    /// </summary>
    Task<bool> GenerateAndSendOtpAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify OTP code for a user.
    /// </summary>
    Task<bool> VerifyOtpAsync(string userId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an OTP was recently sent to prevent spam.
    /// </summary>
    Task<bool> CanRequestNewOtpAsync(string email, CancellationToken cancellationToken = default);
}
