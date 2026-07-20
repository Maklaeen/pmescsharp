using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PmesCSharp.Data;
using PmesCSharp.Models;

namespace PmesCSharp.Services;

public class OtpService : IOtpService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<OtpService> _logger;

    private const int OTP_LENGTH = 6;
    private const int OTP_EXPIRATION_MINUTES = 10;
    private const int OTP_RESEND_WAIT_SECONDS = 60; // Must wait 60 seconds between OTP requests
    private const int MAX_FAILED_ATTEMPTS = 5;

    public OtpService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILogger<OtpService> logger)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<bool> GenerateAndSendOtpAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null)
            {
                _logger.LogWarning("OTP requested for non-existent email: {Email}", email);
                return false;
            }

            // Check if user can request new OTP (rate limiting)
            if (!await CanRequestNewOtpAsync(email, cancellationToken))
            {
                _logger.LogWarning("OTP request rate-limited for user {UserId}", user.Id);
                return false;
            }

            // Invalidate any previous unused OTPs
            var previousOtps = await _db.Set<OtpCode>()
                .Where(o => o.UserId == user.Id && !o.IsUsed)
                .ToListAsync(cancellationToken);

            foreach (var otp in previousOtps)
                otp.IsUsed = true;

            // Generate 6-digit code
            var code = GenerateRandomCode();
            var expiresAt = DateTime.UtcNow.AddMinutes(OTP_EXPIRATION_MINUTES);

            var otpRecord = new OtpCode
            {
                UserId = user.Id,
                Code = code,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };

            _db.Set<OtpCode>().Add(otpRecord);
            await _db.SaveChangesAsync(cancellationToken);

            // Send email with OTP code
            var htmlBody = $"""
                <p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName ?? user.Email)},</p>
                <p>Your one-time login code is:</p>
                <h2 style="font-size: 32px; font-weight: bold; color: #ea580c; letter-spacing: 4px;">{code}</h2>
                <p>This code expires in <strong>10 minutes</strong>.</p>
                <p>If you did not request this code, please ignore this email.</p>
                """;

            await _emailSender.SendAsync(
                user.Email!,
                "Your PMES Login Code",
                htmlBody,
                cancellationToken
            );

            _logger.LogInformation("OTP sent to user {UserId} ({Email})", user.Id, user.Email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and send OTP for email {Email}", email);
            return false;
        }
    }

    public async Task<bool> VerifyOtpAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != OTP_LENGTH)
            {
                _logger.LogWarning("Invalid OTP format for user {UserId}", userId);
                return false;
            }

            var otpRecord = await _db.Set<OtpCode>()
                .FirstOrDefaultAsync(
                    o => o.UserId == userId && o.Code == code && !o.IsUsed,
                    cancellationToken);

            if (otpRecord is null)
            {
                _logger.LogWarning("OTP not found or already used for user {UserId}", userId);
                return false;
            }

            // Check if OTP has expired
            if (DateTime.UtcNow > otpRecord.ExpiresAt)
            {
                _logger.LogWarning("OTP expired for user {UserId}", userId);
                otpRecord.IsUsed = true;
                await _db.SaveChangesAsync(cancellationToken);
                return false;
            }

            // Check failed attempts
            if (otpRecord.FailedAttempts >= MAX_FAILED_ATTEMPTS)
            {
                _logger.LogWarning("Max failed OTP attempts for user {UserId}", userId);
                otpRecord.IsUsed = true;
                await _db.SaveChangesAsync(cancellationToken);
                return false;
            }

            // OTP is valid!
            otpRecord.IsUsed = true;
            otpRecord.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("OTP verified for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying OTP for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> CanRequestNewOtpAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null) return false;

            // Check if there's a recent unused OTP
            var recentOtp = await _db.Set<OtpCode>()
                .Where(o => o.UserId == user.Id && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (recentOtp is null)
                return true; // No recent OTP

            var secondsSinceCreation = (DateTime.UtcNow - recentOtp.CreatedAt).TotalSeconds;
            return secondsSinceCreation >= OTP_RESEND_WAIT_SECONDS;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking OTP rate limit for email {Email}", email);
            return false;
        }
    }

    private static string GenerateRandomCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}
