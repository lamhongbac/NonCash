using FluentAssertions;
using Microsoft.Extensions.Logging;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Services;

namespace NonCash.UnitTests.Services;

public class ConsoleNotificationServiceTests
{
    private sealed class CapturingLogger : ILogger<ConsoleNotificationService>
    {
        public List<string> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(formatter(state, exception));
    }

    private static VoucherReceivedNotification Notification(NotificationChannel channels, string? email = "alice@example.com") => new(
        Email: email,
        PhoneNumber: "0912345678",
        RecipientName: "Alice",
        VoucherName: "Complimentary Voucher",
        FaceValue: 50000m,
        ExpiryDate: new DateTime(2026, 10, 4),
        Channels: channels);

    [Fact]
    public async Task NotifyVoucherReceivedAsync_EmailChannel_LogsSimulatedSend()
    {
        // Arrange
        var logger = new CapturingLogger();
        var sut = new ConsoleNotificationService(logger);

        // Act
        await sut.NotifyVoucherReceivedAsync(Notification(NotificationChannel.Email));

        // Assert
        logger.Entries.Should().ContainSingle(e => e.StartsWith("[EMAIL SIMULATED] Email send executed"))
            .Which.Should().Contain("alice@example.com").And.Contain("0912345678");
    }

    [Fact]
    public async Task NotifyVoucherReceivedAsync_EmailChannelWithoutEmailOnFile_LogsSkip()
    {
        // Arrange
        var logger = new CapturingLogger();
        var sut = new ConsoleNotificationService(logger);

        // Act
        await sut.NotifyVoucherReceivedAsync(Notification(NotificationChannel.Email, email: null));

        // Assert
        logger.Entries.Should().ContainSingle(e => e.StartsWith("[EMAIL SIMULATED] No email on file"));
    }

    [Fact]
    public async Task NotifyVoucherReceivedAsync_NoEmailChannel_DoesNotLogSimulatedSend()
    {
        // Arrange
        var logger = new CapturingLogger();
        var sut = new ConsoleNotificationService(logger);

        // Act
        await sut.NotifyVoucherReceivedAsync(Notification(NotificationChannel.Zalo));

        // Assert
        logger.Entries.Should().NotContain(e => e.StartsWith("[EMAIL SIMULATED]"));
    }

    [Fact]
    public async Task NotifyVoucherReceivedAsync_ParameterlessConstructor_DoesNotThrow()
    {
        // Arrange — existing call sites (tests/fixtures) use the parameterless ctor
        var sut = new ConsoleNotificationService();

        // Act
        var act = () => sut.NotifyVoucherReceivedAsync(Notification(NotificationChannel.Both));

        // Assert
        await act.Should().NotThrowAsync();
    }
}
