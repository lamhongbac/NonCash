using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Services;
using NSubstitute;

namespace NonCash.UnitTests.Services;

public class EmailNotificationServiceTests
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IRepository<EmailLog> _emailLogRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly SmtpOptions _smtpOptions;
    private readonly EmailNotificationService _sut;

    public EmailNotificationServiceTests()
    {
        _userAccountRepository = Substitute.For<IUserAccountRepository>();
        _templateRenderer = Substitute.For<IEmailTemplateRenderer>();
        _emailLogRepository = Substitute.For<IRepository<EmailLog>>();
        _logger = Substitute.For<ILogger<EmailNotificationService>>();
        _configuration = new ConfigurationBuilder().Build();
        _smtpOptions = new SmtpOptions
        {
            Host = "", // Empty host → SendAsync will skip (no real SMTP in unit tests)
            Port = 587,
            EnableSsl = true,
            Username = "test",
            Password = "test",
            FromAddress = "test@test.com",
            FromDisplayName = "Test"
        };

        _sut = new EmailNotificationService(
            Options.Create(_smtpOptions),
            _userAccountRepository,
            _templateRenderer,
            _emailLogRepository,
            _configuration,
            _logger);
    }

    [Fact]
    public async Task NotifyStaffAccountCreatedAsync_SkipsWhenNoEmail()
    {
        var notification = new StaffAccountCreatedNotification(null, "jdoe", "John Doe", "Planner", "TestBrand");

        await _sut.NotifyStaffAccountCreatedAsync(notification);

        // Should not attempt to render or send
        await _templateRenderer.DidNotReceive().RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyStaffAccountCreatedAsync_SkipsWhenEmptyEmail()
    {
        var notification = new StaffAccountCreatedNotification("  ", "jdoe", "John Doe", "Planner", "TestBrand");

        await _sut.NotifyStaffAccountCreatedAsync(notification);

        await _templateRenderer.DidNotReceive().RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyVoucherTransferInitiatedAsync_SkipsWhenNoEmail()
    {
        var notification = new VoucherTransferInitiatedNotification(null, "0901234567", "Recipient", "Sender", 1, DateTime.UtcNow);

        await _sut.NotifyVoucherTransferInitiatedAsync(notification);

        await _templateRenderer.DidNotReceive().RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyPasswordResetAsync_SkipsWhenNoEmail()
    {
        var notification = new PasswordResetNotification(null, "John Doe", "token123", DateTime.UtcNow.AddMinutes(30));

        await _sut.NotifyPasswordResetAsync(notification);

        await _templateRenderer.DidNotReceive().RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyStaffAccountCreatedAsync_RendersTemplateWhenEmailPresent()
    {
        var notification = new StaffAccountCreatedNotification("user@test.com", "jdoe", "John Doe", "Planner", "TestBrand");

        _templateRenderer.RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await _sut.NotifyStaffAccountCreatedAsync(notification);

        await _templateRenderer.Received(1).RenderAsync(
            "StaffAccountCreated",
            Arg.Is<Dictionary<string, string?>>(d =>
                d["FullName"] == "John Doe" &&
                d["Username"] == "jdoe" &&
                d["Role"] == "Planner" &&
                d["BrandName"] == "TestBrand"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyVoucherTransferInitiatedAsync_RendersTemplateWhenEmailPresent()
    {
        var notification = new VoucherTransferInitiatedNotification("recipient@test.com", "0901234567", "Recipient Name", "Sender Name", 3, DateTime.UtcNow);

        _templateRenderer.RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await _sut.NotifyVoucherTransferInitiatedAsync(notification);

        await _templateRenderer.Received(1).RenderAsync(
            "VoucherTransferInitiated",
            Arg.Is<Dictionary<string, string?>>(d =>
                d["RecipientName"] == "Recipient Name" &&
                d["SenderName"] == "Sender Name" &&
                d["VoucherCount"] == "3"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyPasswordResetAsync_RendersTemplateWhenEmailPresent()
    {
        var expiry = new DateTime(2026, 8, 14, 12, 0, 0);
        var notification = new PasswordResetNotification("user@test.com", "John Doe", "abc123token", expiry);

        _templateRenderer.RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await _sut.NotifyPasswordResetAsync(notification);

        await _templateRenderer.Received(1).RenderAsync(
            "PasswordReset",
            Arg.Is<Dictionary<string, string?>>(d =>
                d["FullName"] == "John Doe" &&
                d["ResetToken"] == "abc123token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SkipsWhenSmtpHostEmpty()
    {
        // SMTP host is empty in test setup → should log warning and skip
        var notification = new StaffAccountCreatedNotification("user@test.com", "jdoe", "John Doe", "Planner", "TestBrand");

        _templateRenderer.RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await _sut.NotifyStaffAccountCreatedAsync(notification);

        // Template should be rendered but no email sent (SMTP host empty → skipped)
        await _emailLogRepository.DidNotReceive().AddAsync(Arg.Any<EmailLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAdminNewRegistrationAsync_SkipsWhenNoAdminsWithEmail()
    {
        _userAccountRepository.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<UserAccount, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UserAccount>());

        await _sut.NotifyAdminNewRegistrationAsync(Guid.NewGuid(), "TestCompany");

        await _templateRenderer.DidNotReceive().RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string?>>(), Arg.Any<CancellationToken>());
    }
}
