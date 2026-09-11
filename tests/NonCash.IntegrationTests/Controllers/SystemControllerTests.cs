using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NonCash.API.Controllers;

namespace NonCash.IntegrationTests.Controllers;

/// <summary>Locks the honesty contract of the environment badge: EmailDelivery must mean
/// "a real email would actually leave", not merely "SMTP keys are present".</summary>
public class SystemControllerTests
{
    private static SystemInfoResponse GetInfo(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var ok = new SystemController(config).GetInfo().Result
            .Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeOfType<SystemInfoResponse>().Subject;
    }

    [Fact]
    public void GetInfo_InDev_ReportsEmailDeliveryOff_EvenWhenSmtpIsConfigured()
    {
        var info = GetInfo(new Dictionary<string, string?>
        {
            ["Environment:Name"] = "dev",
            ["Notifications:EmailEnabled"] = "true",
            ["Smtp:Host"] = "smtp.gmail.com"
        });

        info.Environment.Should().Be("dev");
        info.EmailDelivery.Should().BeFalse();
    }

    [Fact]
    public void GetInfo_InPilot_WithSmtpConfigured_ReportsEmailDeliveryOn()
    {
        var info = GetInfo(new Dictionary<string, string?>
        {
            ["Environment:Name"] = "pilot",
            ["Notifications:EmailEnabled"] = "true",
            ["Smtp:Host"] = "smtp.gmail.com"
        });

        info.Environment.Should().Be("pilot");
        info.EmailDelivery.Should().BeTrue();
    }

    [Fact]
    public void GetInfo_InPilot_WithoutAnSmtpHost_ReportsEmailDeliveryOff()
    {
        var info = GetInfo(new Dictionary<string, string?>
        {
            ["Environment:Name"] = "pilot",
            ["Notifications:EmailEnabled"] = "true"
        });

        info.EmailDelivery.Should().BeFalse();
    }
}
