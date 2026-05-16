using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/plans/{planId:guid}")]
[Authorize]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;
    private readonly ICurrentUserService _currentUser;

    // NFR3: Only BrandManager and Admin can execute batch promotion
    private static readonly string[] AllowedRoles = { "BrandManager", "Admin" };

    public PromotionsController(IPromotionService promotionService, ICurrentUserService currentUser)
    {
        _promotionService = promotionService;
        _currentUser = currentUser;
    }

    [HttpPost("promote")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB cap
    public async Task<ActionResult<PromoteResponse>> Promote(
        Guid planId,
        [FromForm] IFormFile? file,
        [FromForm] string? phoneNumbersCsv,
        CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        var role = _currentUser.GetCurrentUserRole();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        if (string.IsNullOrEmpty(role) || !AllowedRoles.Contains(role))
            return Forbid();

        var phones = await ResolvePhoneNumbersAsync(file, phoneNumbersCsv, cancellationToken);
        if (phones.Count == 0)
            return BadRequest(new { error = "EmptyList", message = "No phone numbers were provided." });

        var result = await _promotionService.DistributeAsync(planId, brandId.Value, phones, cancellationToken);
        return MapResult(result);
    }

    [HttpPost("promote/json")]
    public async Task<ActionResult<PromoteResponse>> PromoteJson(
        Guid planId,
        [FromBody] PromoteJsonRequest request,
        CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        var role = _currentUser.GetCurrentUserRole();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        if (string.IsNullOrEmpty(role) || !AllowedRoles.Contains(role))
            return Forbid();

        if (request?.PhoneNumbers == null || request.PhoneNumbers.Count == 0)
            return BadRequest(new { error = "EmptyList", message = "phoneNumbers is required." });

        var result = await _promotionService.DistributeAsync(planId, brandId.Value, request.PhoneNumbers, cancellationToken);
        return MapResult(result);
    }

    private ActionResult<PromoteResponse> MapResult(PromotionResult result)
    {
        if (result.Success)
        {
            return Ok(new PromoteResponse(
                result.DistributedCount,
                result.SkippedCount,
                result.SkippedRecords?.Select(s => new SkippedDto(s.PhoneNumber, s.Reason)).ToList() ?? new List<SkippedDto>()));
        }

        return result.ErrorCode switch
        {
            "NotFound" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
            "Forbidden" => StatusCode(403, new { error = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new
            {
                error = result.ErrorCode ?? "Validation",
                message = result.ErrorMessage,
                skipped = result.SkippedRecords?.Select(s => new SkippedDto(s.PhoneNumber, s.Reason)).ToList()
            })
        };
    }

    private static async Task<List<string>> ResolvePhoneNumbersAsync(
        IFormFile? file,
        string? phoneNumbersCsv,
        CancellationToken cancellationToken)
    {
        var result = new List<string>();

        if (file != null && file.Length > 0)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            string? line;
            var isFirstLine = true;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                // Skip header row if it does not look like a phone (contains letters)
                if (isFirstLine)
                {
                    isFirstLine = false;
                    if (line.Any(char.IsLetter))
                        continue;
                }

                // Take the first column from CSV
                var firstCol = line.Split(',', '\t', ';')[0].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(firstCol))
                    result.Add(firstCol);
            }
        }

        if (!string.IsNullOrWhiteSpace(phoneNumbersCsv))
        {
            foreach (var p in phoneNumbersCsv.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(p))
                    result.Add(p);
            }
        }

        return result;
    }
}

public record PromoteJsonRequest(List<string> PhoneNumbers);

public record PromoteResponse(int DistributedCount, int SkippedCount, List<SkippedDto> SkippedPhones);

public record SkippedDto(string PhoneNumber, string Reason);
