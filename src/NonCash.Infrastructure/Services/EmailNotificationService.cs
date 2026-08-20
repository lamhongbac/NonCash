using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
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
    private const int MaxRetries = 3;
    private readonly SmtpOptions _smtpOptions;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IRepository<EmailLog> _emailLogRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<SmtpOptions> smtpOptions,
        IUserAccountRepository userAccountRepository,
        IEmailTemplateRenderer templateRenderer,
        IRepository<EmailLog> emailLogRepository,
        IConfiguration configuration,
        ILogger<EmailNotificationService> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _templateRenderer = templateRenderer ?? throw new ArgumentNullException(nameof(templateRenderer));
        _emailLogRepository = emailLogRepository;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
            await SendAsync(admin.Email!, subject, body, cancellationToken, "AdminNewRegistration", "NewRegistration");
        }
    }

    public async Task NotifyRegistrationRejectedAsync(string email, string businessName, string? reviewNotes = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogInformation("Registration rejected notification for '{BusinessName}' skipped: no email on file.", businessName);
            return;
        }

        // Always present a reason; fall back to generic wording when the reviewer left no note.
        var reason = string.IsNullOrWhiteSpace(reviewNotes)
            ? "the submitted information did not meet our verification requirements"
            : HtmlEncode(reviewNotes);

        var subject = $"Your NonCash registration for '{businessName}' was not approved";
        var body = await _templateRenderer.RenderAsync("RegistrationRejected", new Dictionary<string, string?>
        {
            ["BusinessName"] = businessName,
            ["Reason"] = reason
        }, cancellationToken);

        await SendAsync(email, subject, body, cancellationToken, "RegistrationRejected", "RegistrationRejected");
    }

    public async Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default)
    {
        var webBaseUrl = _configuration["WebBaseUrl"]?.TrimEnd('/') ?? string.Empty;
        var welcomeUrl = string.IsNullOrEmpty(webBaseUrl)
            ? string.Empty
            : $"{webBaseUrl}/registration-welcome/{requestId}";

        var subject = "Thank you for registering your business with NonCash";
        var body = await _templateRenderer.RenderAsync("ApplicantRegistrationSubmitted", new Dictionary<string, string?>
        {
            ["CompanyName"] = companyName,
            ["RequestId"] = requestId.ToString(),
            ["WelcomeUrl"] = welcomeUrl,
            ["WelcomeLinkHtml"] = string.IsNullOrEmpty(welcomeUrl)
                ? string.Empty
                : $"<p><a href=\"{welcomeUrl}\" style=\"display:inline-block;padding:10px 20px;background-color:#1976d2;color:#fff;text-decoration:none;border-radius:6px;\">View Registration Welcome Page</a></p>"
        }, cancellationToken);

        await SendAsync(email, subject, body, cancellationToken, "ApplicantRegistrationSubmitted", "RegistrationConfirmation");
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

        await SendAsync(notification.Email, subject, body, cancellationToken, "VoucherReceived", "VoucherDistribution");

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
            await SendAsync(email, subject, body, cancellationToken, "AdjustmentPending", "AdjustmentPending");
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

        await SendAsync(notification.RequesterEmail, subject, body, cancellationToken, "AdjustmentReviewed", "AdjustmentReviewed");
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

        await SendAsync(notification.BrandEmail, subject, body, cancellationToken, "CreditsExpiring", "CreditsExpiring");
    }

    public async Task NotifyWelcomeCreditGrantedAsync(WelcomeCreditGrantedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BrandEmail))
        {
            _logger.LogInformation("Welcome credit notification skipped for {BrandName}: no contact email.", notification.BrandName);
            return;
        }

        var expiresAtHtml = notification.ExpiresAt.HasValue
            ? $"<p><strong>Expires at:</strong> {notification.ExpiresAt.Value:yyyy-MM-dd}</p>"
            : string.Empty;

        var subject = $"Welcome to NonCash: {notification.CreditsGranted:N0} credit(s) granted to '{notification.BrandName}'";
        var body = await _templateRenderer.RenderAsync("WelcomeCreditGranted", new Dictionary<string, string?>
        {
            ["BrandName"] = notification.BrandName,
            ["CreditsGranted"] = notification.CreditsGranted.ToString("N0"),
            ["ExpiresAt"] = expiresAtHtml
        }, cancellationToken);

        await SendAsync(notification.BrandEmail, subject, body, cancellationToken, "WelcomeCreditGranted", "WelcomeCreditGranted");
    }

    public async Task NotifyBrandCreatedAsync(BrandCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BrandEmail))
        {
            _logger.LogInformation("Brand-created notification skipped for {BrandName}: no contact email.", notification.BrandName);
            return;
        }

        var subject = $"Your business '{notification.BrandName}' is now active on NonCash";
        var body = await _templateRenderer.RenderAsync("BrandCreated", new Dictionary<string, string?>
        {
            ["BrandName"] = notification.BrandName,
            ["BusinessName"] = notification.BusinessName,
            ["TaxCode"] = notification.TaxCode
        }, cancellationToken);

        await SendAsync(notification.BrandEmail, subject, body, cancellationToken, "BrandCreated", "BrandCreated");
    }

    public async Task NotifyBusinessActivatedAsync(BusinessActivatedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BusinessEmail))
        {
            _logger.LogInformation("Business-activated notification skipped for {BusinessName}: no contact email.", notification.BusinessName);
            return;
        }

        string brandInfoHtml;
        if (!string.IsNullOrWhiteSpace(notification.BrandName) && notification.CreditsGranted > 0)
        {
            var expiresAtHtml = notification.ExpiresAt.HasValue
                ? $"<p><strong>Welcome credits expire at:</strong> {notification.ExpiresAt.Value:yyyy-MM-dd}</p>"
                : string.Empty;

            brandInfoHtml = $@"
                <div style=""background-color: #f1f8e9; border: 1px solid #c5e1a5; border-radius: 6px; padding: 12px 16px; margin: 16px 0;"">
                    <p style=""margin: 0;""><strong>Welcome credit policy for new brands:</strong> your new brand <strong>{notification.BrandName}</strong> has been granted <strong>{notification.CreditsGranted:N0}</strong> welcome credit(s). Each voucher consumes 1 credit, so you can issue up to {notification.CreditsGranted:N0} voucher(s) with this grant.</p>
                    {expiresAtHtml}
                </div>
                <p>You can sign in with your registered account and start creating voucher plans and distributing vouchers to your customers.</p>";
        }
        else
        {
            brandInfoHtml = "<p>Our team will contact you shortly to set up your first brand and user account.</p>";
        }

        var subject = $"Welcome to NonCash — '{notification.BusinessName}' is now active";
        var body = await _templateRenderer.RenderAsync("ActiveBusiness", new Dictionary<string, string?>
        {
            ["BusinessName"] = notification.BusinessName,
            ["BrandInfoHtml"] = brandInfoHtml
        }, cancellationToken);

        await SendAsync(notification.BusinessEmail, subject, body, cancellationToken, "ActiveBusiness", "BusinessActivated");
    }

    public async Task NotifyContractSentAsync(ContractSentNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BusinessEmail))
        {
            _logger.LogInformation("Contract-sent notification skipped for {BusinessName}: no contact email.", notification.BusinessName);
            return;
        }

        var expiryHtml = notification.WelcomeCreditExpiryMonths.HasValue
            ? $"<p><strong>Credit expiry:</strong> {notification.WelcomeCreditExpiryMonths.Value} month(s) after activation</p>"
            : string.Empty;

        var subject = $"NonCash contract for '{notification.BusinessName}'";
        var body = await _templateRenderer.RenderAsync("ContractSent", new Dictionary<string, string?>
        {
            ["BusinessName"] = notification.BusinessName,
            ["BrandName"] = notification.BrandName,
            ["PolicyTemplateName"] = notification.PolicyTemplateName,
            ["WelcomeCredits"] = notification.WelcomeCredits.ToString("N0"),
            ["ExpiryHtml"] = expiryHtml,
            ["ContractHtml"] = notification.ContractHtml
        }, cancellationToken);

        await SendAsync(notification.BusinessEmail, subject, body, cancellationToken, "ContractSent", "ContractSent");
    }

    public async Task NotifyCreditPurchasedAsync(CreditPurchasedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BrandEmail))
        {
            _logger.LogInformation("Credit purchase receipt skipped for {BrandName}: no contact email.", notification.BrandName);
            return;
        }

        var expiresAtHtml = notification.ExpiresAt.HasValue
            ? $"<p><strong>Expires at:</strong> {notification.ExpiresAt.Value:yyyy-MM-dd}</p>"
            : string.Empty;

        var subject = $"Credit purchase receipt: {notification.Amount:N0} credit(s)";
        var body = await _templateRenderer.RenderAsync("CreditPurchased", new Dictionary<string, string?>
        {
            ["BrandName"] = notification.BrandName,
            ["Amount"] = notification.Amount.ToString("N0"),
            ["TotalPaidVnd"] = notification.TotalPaidVnd.ToString("N0"),
            ["Reference"] = notification.Reference ?? "N/A",
            ["ExpiresAt"] = expiresAtHtml
        }, cancellationToken);

        await SendAsync(notification.BrandEmail, subject, body, cancellationToken, "CreditPurchased", "CreditPurchased");
    }

    public async Task NotifyLowCreditBalanceAsync(LowCreditBalanceNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BrandEmail))
        {
            _logger.LogInformation("Low balance warning skipped for {BrandName}: no contact email.", notification.BrandName);
            return;
        }

        var subject = $"Low credit balance warning: {notification.CurrentBalance:N0} credit(s) remaining";
        var body = await _templateRenderer.RenderAsync("LowCreditBalance", new Dictionary<string, string?>
        {
            ["BrandName"] = notification.BrandName,
            ["CurrentBalance"] = notification.CurrentBalance.ToString("N0"),
            ["Threshold"] = notification.Threshold.ToString("N0")
        }, cancellationToken);

        await SendAsync(notification.BrandEmail, subject, body, cancellationToken, "LowCreditBalance", "LowCreditBalance");
    }

    public async Task NotifyCreditsForfeitedAsync(CreditsForfeitedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BrandEmail))
        {
            _logger.LogInformation("Credits forfeited notification skipped for {BrandName}: no contact email.", notification.BrandName);
            return;
        }

        var subject = $"Credits forfeited: {notification.ForfeitedCredits:N0} credit(s) expired";
        var body = await _templateRenderer.RenderAsync("CreditsForfeited", new Dictionary<string, string?>
        {
            ["BrandName"] = notification.BrandName,
            ["ForfeitedCredits"] = notification.ForfeitedCredits.ToString("N0"),
            ["ExpiredAt"] = notification.ExpiredAt.ToString("yyyy-MM-dd")
        }, cancellationToken);

        await SendAsync(notification.BrandEmail, subject, body, cancellationToken, "CreditsForfeited", "CreditsForfeited");
    }

    public async Task NotifyPlanReviewedAsync(PlanReviewedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.CreatorEmail))
        {
            _logger.LogInformation("Plan review notification skipped for {PlanDisplayName}: creator has no email.", notification.PlanDisplayName);
            return;
        }

        var outcome = notification.Approved ? "Approved" : "Rejected";
        var headerColor = notification.Approved ? "#388e3c" : "#d32f2f";
        var publishDateHtml = notification.Approved && notification.PublishDate.HasValue
            ? $"<p><strong>Publish date:</strong> {notification.PublishDate.Value:yyyy-MM-dd}</p>"
            : string.Empty;
        var reviewNotesHtml = string.IsNullOrWhiteSpace(notification.ReviewNotes)
            ? string.Empty
            : $"<p><strong>Reviewer note:</strong> {HtmlEncode(notification.ReviewNotes)}</p>";

        var subject = $"Voucher plan {outcome.ToLowerInvariant()}: {notification.PlanDisplayName}";
        var body = await _templateRenderer.RenderAsync("PlanReviewed", new Dictionary<string, string?>
        {
            ["Outcome"] = outcome,
            ["HeaderColor"] = headerColor,
            ["PlanDisplayName"] = notification.PlanDisplayName,
            ["PublishDate"] = publishDateHtml,
            ["ReviewNotes"] = reviewNotesHtml
        }, cancellationToken);

        await SendAsync(notification.CreatorEmail, subject, body, cancellationToken, "PlanReviewed", "PlanReviewed");
    }

    public async Task NotifyStaffAccountCreatedAsync(StaffAccountCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.UserEmail))
        {
            _logger.LogInformation("Staff account created notification skipped for {Username}: no email on file.", notification.Username);
            return;
        }

        var subject = $"Your NonCash account has been created: {notification.Role}";
        var body = await _templateRenderer.RenderAsync("StaffAccountCreated", new Dictionary<string, string?>
        {
            ["FullName"] = notification.FullName,
            ["Username"] = notification.Username,
            ["Role"] = notification.Role,
            ["BrandName"] = notification.BrandName ?? "NonCash Platform"
        }, cancellationToken);

        await SendAsync(notification.UserEmail, subject, body, cancellationToken, "StaffAccountCreated", "StaffAccountCreated");
    }

    public async Task NotifyVoucherTransferInitiatedAsync(VoucherTransferInitiatedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.RecipientEmail))
        {
            _logger.LogInformation("Voucher transfer notification skipped for {Phone}: no email on file.", notification.RecipientPhone);
            return;
        }

        var subject = $"You've received {notification.VoucherCount} voucher(s) via transfer";
        var body = await _templateRenderer.RenderAsync("VoucherTransferInitiated", new Dictionary<string, string?>
        {
            ["RecipientName"] = notification.RecipientName,
            ["SenderName"] = notification.SenderName,
            ["VoucherCount"] = notification.VoucherCount.ToString(),
            ["TransferredAt"] = notification.TransferredAt.ToString("yyyy-MM-dd HH:mm")
        }, cancellationToken);

        await SendAsync(notification.RecipientEmail, subject, body, cancellationToken, "VoucherTransferInitiated", "VoucherTransfer");
    }

    public async Task NotifyPasswordResetAsync(PasswordResetNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.UserEmail))
        {
            _logger.LogInformation("Password reset notification skipped for {FullName}: no email on file.", notification.FullName);
            return;
        }

        var subject = "NonCash password reset request";
        var body = await _templateRenderer.RenderAsync("PasswordReset", new Dictionary<string, string?>
        {
            ["FullName"] = notification.FullName,
            ["ResetToken"] = notification.ResetToken,
            ["TokenExpiry"] = notification.TokenExpiry.ToString("yyyy-MM-dd HH:mm")
        }, cancellationToken);

        await SendAsync(notification.UserEmail, subject, body, cancellationToken, "PasswordReset", "PasswordReset");
    }

    private async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken, string templateName = "", string notificationType = "")
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host) || string.IsNullOrWhiteSpace(_smtpOptions.FromAddress))
        {
            _logger.LogWarning("SMTP is not configured. Skipping email to {ToAddress}.", toAddress);
            return;
        }

        var retryCount = 0;
        Exception? lastException = null;

        for (retryCount = 0; retryCount <= MaxRetries; retryCount++)
        {
            try
            {
                if (retryCount > 0)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                    _logger.LogInformation("Retrying email to {ToAddress} (attempt {Attempt}/{MaxRetries}) after {Delay}s.", toAddress, retryCount, MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }

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

                await LogEmailAsync(toAddress, subject, templateName, notificationType, success: true, errorMessage: null, retryCount, cancellationToken);
                return;
            }
            catch (SmtpException ex) when (IsTransient(ex.StatusCode))
            {
                lastException = ex;
                _logger.LogWarning(ex, "Transient SMTP error sending to {ToAddress}. Retry {Attempt}/{MaxRetries}.", toAddress, retryCount + 1, MaxRetries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToAddress}.", toAddress);
                await LogEmailAsync(toAddress, subject, templateName, notificationType, success: false, errorMessage: ex.Message, retryCount, cancellationToken);
                return;
            }
        }

        // All retries exhausted
        _logger.LogError(lastException, "Failed to send email to {ToAddress} after {MaxRetries} retries.", toAddress, MaxRetries);
        await LogEmailAsync(toAddress, subject, templateName, notificationType, success: false, errorMessage: $"Failed after {MaxRetries} retries: {lastException?.Message}", retryCount - 1, cancellationToken);
    }

    private static bool IsTransient(SmtpStatusCode statusCode) =>
        statusCode is SmtpStatusCode.ServiceNotAvailable
            or SmtpStatusCode.ServiceClosingTransmissionChannel
            or SmtpStatusCode.GeneralFailure;

    private async Task LogEmailAsync(string toAddress, string subject, string templateName, string notificationType, bool success, string? errorMessage, int retryCount, CancellationToken cancellationToken)
    {
        try
        {
            var log = new EmailLog
            {
                ToAddress = toAddress,
                Subject = subject,
                TemplateName = templateName,
                NotificationType = notificationType,
                Success = success,
                ErrorMessage = errorMessage?.Length > 2000 ? errorMessage[..2000] : errorMessage,
                RetryCount = retryCount,
                SentAt = DateTime.UtcNow
            };

            await _emailLogRepository.AddAsync(log, cancellationToken);
            await _emailLogRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Logging failures must never break the notification flow.
            _logger.LogError(ex, "Failed to persist email log for {ToAddress}.", toAddress);
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
