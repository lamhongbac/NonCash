using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "NonCash";
}

public class EmailNotificationService : INotificationService
{
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(IOptions<SmtpOptions> smtpOptions, ILogger<EmailNotificationService> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    public Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default)
    {
        var subject = $"New business registration: {companyName}";
        var body = $"A new business registration has been submitted.\n\nCompany: {companyName}\nRequest ID: {requestId}\n\nPlease review it in the admin portal.";
        return SendAsync(_smtpOptions.FromAddress, subject, body, cancellationToken);
    }

    public Task NotifyApplicantReviewResultAsync(Guid userId, string brandName, bool approved, CancellationToken cancellationToken = default)
    {
        // Email address is not available in this signature; log and defer to a future enhancement.
        _logger.LogInformation("Applicant review result notification for user {UserId}, brand {BrandName}, approved={Approved}.", userId, brandName, approved);
        return Task.CompletedTask;
    }

    public Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default)
    {
        var subject = "Thank you for registering your business with NonCash";
        var body = $"Dear {companyName},\n\nThank you for submitting your business registration.\n\nYour request ID is: {requestId}\n\nWe will review your application and notify you once it is approved.\n\nBest regards,\nNonCash Team";
        return SendAsync(email, subject, body, cancellationToken);
    }

    public async Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification.Channels.HasFlag(NotificationChannel.Email))
        {
            if (string.IsNullOrWhiteSpace(notification.Email))
            {
                _logger.LogInformation("Voucher notification skipped for {Phone}: no email on file.", notification.PhoneNumber);
            }
            else
            {
                var subject = $"You've received a voucher: {notification.VoucherName ?? "NonCash voucher"}";
                var body = $"Dear {notification.RecipientName},\n\n" +
                           $"A voucher has been added to your NonCash wallet.\n\n" +
                           $"Voucher: {notification.VoucherName ?? "NonCash voucher"}\n" +
                           $"Value: {notification.FaceValue:N0}\n" +
                           $"Valid until: {notification.ExpiryDate:yyyy-MM-dd}\n\n" +
                           $"Log in with your phone number {notification.PhoneNumber} to view and redeem it.\n\n" +
                           $"Best regards,\nNonCash Team";
                await SendAsync(notification.Email, subject, body, cancellationToken);
            }
        }

        if (notification.Channels.HasFlag(NotificationChannel.Zalo))
        {
            // Zalo ZNS delivery activates once the Official Account and templates are approved.
            _logger.LogInformation("Zalo ZNS not yet onboarded. Zalo notification for {Phone} skipped.", notification.PhoneNumber);
        }
    }

    private async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host) || string.IsNullOrWhiteSpace(_smtpOptions.FromAddress))
        {
            _logger.LogWarning("SMTP is not configured. Skipping email to {ToAddress}.", toAddress);
            return;
        }

        try
        {
            using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
            {
                EnableSsl = _smtpOptions.EnableSsl,
                Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password)
            };

            var from = new MailAddress(_smtpOptions.FromAddress, _smtpOptions.FromDisplayName);
            var message = new MailMessage(from, new MailAddress(toAddress))
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email sent to {ToAddress}: {Subject}", toAddress, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToAddress}.", toAddress);
        }
    }
}
