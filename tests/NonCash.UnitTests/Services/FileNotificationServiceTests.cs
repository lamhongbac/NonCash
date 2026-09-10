using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Services;
using NSubstitute;

namespace NonCash.UnitTests.Services;

/// <summary>CR-2026-09-10-30 row 8: the dev notification sink writes to a file, and the lines must
/// carry the link URLs that the email audit table does not store.</summary>
public class FileNotificationServiceTests : IDisposable
{
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), $"noncash-notifications-{Guid.NewGuid():N}.log");

    private FileNotificationService CreateSut()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration["Notifications:LogFile"].Returns(_logPath);
        return new FileNotificationService(configuration);
    }

    private string ReadLog() => File.Exists(_logPath) ? File.ReadAllText(_logPath) : string.Empty;

    [Fact]
    public async Task GiftSent_WritesRecipientAndMagicLink()
    {
        var sut = CreateSut();

        await sut.NotifyVoucherTransferInitiatedAsync(new VoucherTransferInitiatedNotification(
            "b@example.com", "0363464997", "B", "A", 1, new DateTime(2026, 9, 10, 3, 0, 0),
            "https://localhost:7162/member/welcome?token=abc"));

        ReadLog().Should().Contain("[GiftSent]")
            .And.Contain("b@example.com")
            .And.Contain("0363464997")
            .And.Contain("magicLink=https://localhost:7162/member/welcome?token=abc");
    }

    [Fact]
    public async Task GiftSent_WithoutEmailOnFile_StillRecordsThePhone()
    {
        var sut = CreateSut();

        await sut.NotifyVoucherTransferInitiatedAsync(new VoucherTransferInitiatedNotification(
            null, "0363464997", "0363464997", "A", 1, new DateTime(2026, 9, 10, 3, 0, 0)));

        ReadLog().Should().Contain("to=n/a").And.Contain("phone=0363464997");
    }

    [Fact]
    public async Task GiftAccepted_WritesTheThankYouNote()
    {
        var sut = CreateSut();

        await sut.NotifyGiftAcceptedAsync(new GiftAcceptedNotification(
            "a@example.com", "A", "B", "Cảm ơn món quà của bạn!", "Coffee voucher", 50000m,
            new DateTime(2026, 9, 10, 4, 0, 0)));

        ReadLog().Should().Contain("[GiftAccepted]")
            .And.Contain("a@example.com")
            .And.Contain("Cảm ơn món quà của bạn!");
    }

    [Fact]
    public async Task GiftDeclined_WritesTheReason()
    {
        var sut = CreateSut();

        await sut.NotifyGiftDeclinedAsync(new GiftDeclinedNotification(
            "a@example.com", "A", "B", "Wrong brand for me", "Coffee voucher", 50000m,
            new DateTime(2026, 9, 10, 4, 0, 0)));

        ReadLog().Should().Contain("[GiftDeclined]").And.Contain("reason=\"Wrong brand for me\"");
    }

    [Fact]
    public async Task GiftExpired_WritesTheHoldingWindow()
    {
        var sut = CreateSut();

        await sut.NotifyGiftExpiredAsync(new GiftExpiredNotification(
            "a@example.com", "A", "B", "0363464997", "Coffee voucher", 50000m, 7,
            new DateTime(2026, 9, 17, 4, 0, 0)));

        ReadLog().Should().Contain("[GiftExpired]").And.Contain("heldDays=7").And.Contain("phone=0363464997");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task GiftMessage_WritesWhoWroteWhatAboutWhichGift()
    {
        var sut = CreateSut();

        await sut.NotifyGiftMessageAsync(new GiftMessageNotification(
            "b@example.com", "B", "A", "See you on Saturday!", "Coffee voucher", 50000m,
            new DateTime(2026, 9, 10, 6, 0, 0)));

        ReadLog().Should().Contain("[GiftMessage]")
            .And.Contain("author=\"A\"")
            .And.Contain("body=\"See you on Saturday!\"")
            .And.Contain("voucher=\"Coffee voucher\"")
            .And.Contain("sentAt=2026-09-10 06:00");
    }

    [Fact]
    [Trait("Category", "CR-2026-09-10-31")]
    public async Task GiftMessage_WithoutAnEmailOnFile_StillRecordsTheConversation()
    {
        var sut = CreateSut();

        await sut.NotifyGiftMessageAsync(new GiftMessageNotification(
            null, "B", "A", "Hello?", "Coffee voucher", 50000m,
            new DateTime(2026, 9, 10, 6, 0, 0)));

        ReadLog().Should().Contain("to=n/a").And.Contain("body=\"Hello?\"");
    }

    [Fact]
    public async Task SignInLink_WritesTheLinkUrl()
    {
        var sut = CreateSut();

        await sut.NotifySignInLinkAsync(new SignInLinkNotification(
            "a@example.com", "A", "https://localhost:7162/member/welcome?token=xyz",
            new DateTime(2026, 9, 10, 5, 0, 0)));

        ReadLog().Should().Contain("signInLink=https://localhost:7162/member/welcome?token=xyz");
    }

    [Fact]
    public void Ctor_WithoutConfiguration_FallsBackToLogsFolder()
    {
        var sut = new FileNotificationService();

        sut.LogPath.Should().EndWith(Path.Combine("logs", "notifications.log"));
    }

    public void Dispose()
    {
        if (File.Exists(_logPath))
            File.Delete(_logPath);
    }
}
