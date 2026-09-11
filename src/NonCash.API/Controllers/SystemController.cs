using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Configuration;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/system")]
public class SystemController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public SystemController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Exposes the current runtime mode so the web UI can display it.
    /// Anonymous on purpose: the mode badge must render even before login.
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public ActionResult<SystemInfoResponse> GetInfo()
    {
        var environment = _configuration.GetSection(EnvironmentConfig.SectionName).Get<EnvironmentConfig>() ?? new EnvironmentConfig();
        var smtpHost = _configuration["Smtp:Host"];
        var emailEnabled = _configuration.GetValue<bool?>("Notifications:EmailEnabled") ?? !environment.IsDev;

        // The kill-switch suppresses every send in dev, so "delivery on" must mean configured AND not dev.
        return Ok(new SystemInfoResponse(
            environment.Name,
            EmailDelivery: emailEnabled && !environment.IsDev && !string.IsNullOrWhiteSpace(smtpHost)));
    }
}

public record SystemInfoResponse(string Environment, bool EmailDelivery);
