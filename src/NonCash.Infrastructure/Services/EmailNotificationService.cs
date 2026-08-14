using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NonCash.Core.Entities;
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
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<SmtpOptions> smtpOptions,
        IUserAccountRepository userAccountRepository,
        IEmailTemplateRenderer templateRenderer,
        ILogger<EmailNotificationService> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _templateRenderer = templateRenderer ?? throw new ArgumentNullException(nameof(templateRenderer));
        _logger = logger;
    }

    public async Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default)
    {
        var admins = (await _userAccountRepository.FindAsync(
            u => u.Role == UserRole.Admin && u.Status == UserStatus.Active && !string.IsNullOrEmpty(u.Email),
            cancellationToken)).ToList();

        if (admins.Count == 0)
        {
            _logger.LogWarning("New registration notification for {CompanyName} has no active admin recipients with email.", companyName);
            return;
        }

        var subject = $"New business registration: {companyName}";
        var body = await _templateRenderer.RenderAsync("AdminNewRegistration", new Dictionary<string, string?>
        {
            ["CompanyName"] = companyName,
            ["RequestId"] = requestId.ToString()
        }, cancellationToken);

        foreach (var admin in admins)
        {
            await SendAsync(admin.Email!, subject, body, cancellationToken);
        }
    }

    public async Task NotifyApplicantReviewResultAsync(Guid userId, string brandName, bool approved, string? reviewNotes = null, CancellationToken cancellationToken = default)
    {
        var user = await _userAccountRepository.GetByIdAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(user?.Email))
        {
            _logger.LogInformation("Applicant review result notification for user {UserId} skipped: no email on file.", userId);
            return;
        }

        var outcome = approved ? "Approved" : "Rejected";
        var headerColor = approved ? "#388e3c" : "#d32f2f";
        var reviewNoteHtml = string.IsNullOrWhiteSpace(reviewNotes)
            ? string.Empty
            : $"<p><strong>Reviewer note:</strong> {HtmlEncode(reviewNotes)}</p>";

        var subject = $"Your NonCash registration has been {outcome.ToLowerInvariant()}";
        var body = await _templateRenderer.RenderAsync("ApplicantReviewResult", new Dictionary<string, string?>
        {
            ["BrandName"] = brandName,
            ["Outcome"] = outcome,
            ["HeaderColor"] = headerColor,
            ["ReviewNote"] = reviewNoteHtml
        }, cancellationToken);

        await SendAsync(user.Email, subject, body, cancellationToken);
    }

    public async Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default)
    {
        var subject = "Thank you for registering your business with NonCash";
        var body = await _templateRenderer.RenderAsync("ApplicantRegistrationSubmitted", new Dictionary<string, string?>
        {
            ["CompanyName"] = companyName,
            ["RequestId"] = requestId.ToString()
        }, cancellationToken);

        await SendAsync(email, subject, body, cancellationToken);
    }

    public async Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default)
    {
        if (!notification.Channels.HasFlag(NotificationChannel.Email))
            return;

        if (string.IsNullOrWhiteSpace(notification.Email))
        {
            _logger.LogInformation("Voucher notification skipped for {Phone}: no email on file.", notification.PhoneNumber);
            return;
        }

        var subject = $"You've received a voucher: {notification.VoucherName ?? "NonCash voucher"}";
        var body = await _templateRenderer.RenderAsync("VoucherReceived", new Dictionary<string, string?>
        {
            ["RecipientName"] = notification.RecipientName,
            ["VoucherName"] = notification.VoucherName ?? "NonCash voucher",
            ["FaceValue"] = notification.FaceValue.ToString("N0"),
            ["ExpiryDate"] = notification.ExpiryDate.ToString("yyyy-MM-dd"),
            ["PhoneNumber"] = notification.PhoneNumber
        }, cancellationToken);

        await SendAsync(notification.Email, subject, body, cancellationToken);

        if (notification.Channels.HasFlag(NotificationChannel.Zalo))
        {
            // Zalo ZNS delivery activates once the Official Account and templates are approved.
            _logger.LogInformation("Zalo ZNS not yet onboarded. Zalo notification for {Phone} skipped.", notification.PhoneNumber);
        }
    }

    public async Task NotifyAdjustmentPendingAsync(AdjustmentPendingNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification.ApproverEmails.Count == 0)
        {
            _logger.LogWarning("Adjustment {RequestId} pending approval but no FinancialController emails found.", notification.RequestId);
            return;
        }

        var subject = $"Credit adjustment pending approval: {notification.AdjustmentType} {notification.Amount:N0} for {notification.BrandName}";
        var body = await _templateRenderer.RenderAsync("AdjustmentPending", new Dictionary<string, string?>
        {
            ["BrandName"] = notification.BrandName,
            ["AdjustmentType"] = notification.AdjustmentType,
            ["Amount"] = notification.Amount.ToString("N0"),
            ["RequestedByName"] = notification.RequestedByName,
            ["RequestId"] = notification.RequestId.ToString()
        }, cancellationToken);

        foreach (var email in notification.ApproverEmails)
        {
            await SendAsync(email, subject, body, cancellationToken);
        }
    }

    public async Task NotifyAdjustmentReviewedAsync(AdjustmentReviewedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.RequesterEmail))
        {
            _logger.LogInformation("Adjustment {RequestId} reviewed but requester has no email on file.", notification.RequestId);
            return;
        }

        var outcome = notification.Approved ? "Approved" : "Rejected";
        var headerColor = notification.Approved ? "#388e3c" : "#d32f2f";
        var reviewNoteHtml = string.IsNullOrWhiteSpace(notification.ReviewNote)
            ? string.Empty
            : $"<p><strong>Reviewer note:</strong> {HtmlEncode(notification.ReviewNote)}</p>";

        var subject = $"Credit adjustment {outcome.ToLowerInvariant()}: {notification.AdjustmentType} {notification.Amount:N0} for {notification.BrandName}";
        var body = await _templateRenderer.RenderAsync("AdjustmentReviewed", new Dictionary<string, string?>
        {
            ["Outcome"] = outcome,
            ["HeaderColor"] = headerColor,
            ["BrandName"] = notification.BrandName,
            ["AdjustmentType"] = notification.AdjustmentType,
            ["Amount"] = notification.Amount.ToString("N0"),
            ["RequestId"] = notification.RequestId.ToString(),
            ["ReviewNote"] = reviewNoteHtml
        }, cancellationToken);

        await SendAsync(notification.RequesterEmail, subject, body, cancellationToken);
    }

    public async Task NotifyCreditsExpiringAsync(CreditsExpiringNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BrandEmail))
        {
            _logger.LogInformation("Credit expiry warning skipped for {BrandName}: no contact email.", notification.BrandName);
            return;
        }

        var subject = $"NonCash credits expiring soon: {notification.ExpiringCredits:N0} credit(s) on {notification.ExpiresAt:yyyy-MM-dd}";
        var body = await _templateRenderer.RenderAsync("CreditsExpiring", new Dictionary<string, string?>
        {
            ["BrandName"] = notification.BrandName,
            ["ExpiringCredits"] = notification.ExpiringCredits.ToString("N0"),
            ["DaysLeft"] = notification.DaysLeft.ToString(),
            ["ExpiresAt"] = notification.ExpiresAt.ToString("yyyy-MM-dd")
        }, cancellationToken);

        await SendAsync(notification.BrandEmail, subject, body, cancellationToken);
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
                IsBodyHtml = true
            };

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email sent to {ToAddress}: {Subject}", toAddress, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToAddress}.", toAddress);
        }
    }

    private static string HtmlEncode(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
