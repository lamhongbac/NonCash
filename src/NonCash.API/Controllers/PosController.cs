using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/pos")]
// NOTE: No [Authorize]. POS endpoints are gated by ApiKeyMiddleware (X-API-Key).
public class PosController : ControllerBase
{
    private readonly IPosService _posService;

    public PosController(IPosService posService)
    {
        _posService = posService;
    }

    /// <summary>
    /// Stateless voucher verification. AC1-AC5.
    /// Header: X-API-Key (validated by ApiKeyMiddleware).
    /// </summary>
    [HttpPost("verify")]
    public async Task<ActionResult<PosVerifyResponse>> Verify(
        [FromBody] PosVerifyRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.VoucherCode) || request.OutletId == Guid.Empty)
        {
            return Ok(new PosVerifyResponse("Invalid", "BadRequest", null));
        }

        // Cross-check: the supplied OutletId in body must match the API-key-resolved outlet.
        // Prevents an outlet's API key from being used to query a different outlet.
        var middlewareOutletId = HttpContext.Items["pos.outlet_id"] as Guid?;
        if (middlewareOutletId == null || middlewareOutletId.Value != request.OutletId)
        {
            return Ok(new PosVerifyResponse("Invalid", "OutletNotAuthorized", null));
        }

        var result = await _posService.VerifyAsync(request.VoucherCode, request.OutletId, cancellationToken);

        var info = result.VoucherInfo == null
            ? null
            : new PosVoucherInfoDto(
                result.VoucherInfo.FaceValue,
                result.VoucherInfo.ValueType,
                result.VoucherInfo.ExpiryDate,
                result.VoucherInfo.BrandName,
                result.VoucherInfo.SerialNo);

        return Ok(new PosVerifyResponse(result.Status, result.Reason, info));
    }

    /// <summary>
    /// Verifies and atomically locks the voucher (Pending → InUse). AC1-AC4.
    /// Idempotent against (voucherId, outletId, billNumber).
    /// </summary>
    [HttpPost("lock")]
    public async Task<ActionResult<PosLockResponse>> Lock(
        [FromBody] PosLockRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(request.VoucherCode)
            || request.OutletId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.BillNumber))
        {
            return Ok(new PosLockResponse("Invalid", "BadRequest", null, null));
        }

        var middlewareOutletId = HttpContext.Items["pos.outlet_id"] as Guid?;
        if (middlewareOutletId == null || middlewareOutletId.Value != request.OutletId)
        {
            return Ok(new PosLockResponse("Invalid", "OutletNotAuthorized", null, null));
        }

        var result = await _posService.LockAsync(request.VoucherCode, request.OutletId, request.BillNumber, cancellationToken);

        var info = result.VoucherInfo == null
            ? null
            : new PosVoucherInfoDto(
                result.VoucherInfo.FaceValue,
                result.VoucherInfo.ValueType,
                result.VoucherInfo.ExpiryDate,
                result.VoucherInfo.BrandName,
                result.VoucherInfo.SerialNo);

        if (result.Status == "AlreadyInUse")
        {
            return StatusCode(StatusCodes.Status409Conflict,
                new PosLockResponse(result.Status, result.Reason, null, null));
        }

        return Ok(new PosLockResponse(result.Status, result.Reason, result.LockId, info));
    }

    /// <summary>
    /// Permanently commits a previously-locked voucher (InUse → Complete) and logs a VoucherUsage.
    /// Idempotent on TransactionId. AC1-AC5.
    /// </summary>
    [HttpPost("commit")]
    public async Task<ActionResult<PosCommitResponse>> Commit(
        [FromBody] PosCommitRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null
            || request.LockId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return Ok(new PosCommitResponse("Invalid", "BadRequest", null));
        }

        // POS ID = the API-key-resolved outlet (per spec: "POS ID comes from the API Key context").
        var middlewareOutletId = HttpContext.Items["pos.outlet_id"] as Guid?;
        if (middlewareOutletId == null)
        {
            return Ok(new PosCommitResponse("Invalid", "OutletNotAuthorized", null));
        }

        var result = await _posService.CommitAsync(
            request.LockId,
            request.TransactionId,
            request.AmountUsed,
            middlewareOutletId.Value,
            cancellationToken);

        if (result.Status == "LockExpired")
        {
            return StatusCode(StatusCodes.Status409Conflict,
                new PosCommitResponse(result.Status, result.Reason, null));
        }

        // AlreadyComplete is HTTP 200 per spec (idempotent semantic).
        return Ok(new PosCommitResponse(result.Status, result.Reason, result.Message));
    }

    /// <summary>
    /// Rolls back a locked voucher (compensating transaction for Lock). AC1-AC4.
    /// </summary>
    [HttpPost("rollback")]
    public async Task<ActionResult<PosRollbackResponse>> Rollback(
        [FromBody] PosRollbackRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.LockId == Guid.Empty)
        {
            return Ok(new PosRollbackResponse("Invalid", "BadRequest", null));
        }

        // Gate: caller must come through ApiKeyMiddleware (any active outlet API key).
        var middlewareOutletId = HttpContext.Items["pos.outlet_id"] as Guid?;
        if (middlewareOutletId == null)
        {
            return Ok(new PosRollbackResponse("Invalid", "OutletNotAuthorized", null));
        }

        var result = await _posService.RollbackAsync(request.LockId, cancellationToken);

        if (result.Status == "AlreadyCompleted")
        {
            return StatusCode(StatusCodes.Status409Conflict,
                new PosRollbackResponse(result.Status, result.Reason, null));
        }

        // Success + AlreadyReleased both return HTTP 200 (idempotent).
        return Ok(new PosRollbackResponse(result.Status, result.Reason, result.Message));
    }
}

public record PosVerifyRequest(string VoucherCode, Guid OutletId);

public record PosVerifyResponse(string Status, string? Reason, PosVoucherInfoDto? VoucherInfo);

public record PosLockRequest(string VoucherCode, Guid OutletId, string BillNumber);

public record PosLockResponse(string Status, string? Reason, Guid? LockId, PosVoucherInfoDto? VoucherInfo);

public record PosCommitRequest(Guid LockId, string TransactionId, decimal AmountUsed);

public record PosCommitResponse(string Status, string? Reason, string? Message);

public record PosRollbackRequest(Guid LockId);

public record PosRollbackResponse(string Status, string? Reason, string? Message);

public record PosVoucherInfoDto(
    decimal FaceValue,
    string ValueType,
    DateTime ExpiryDate,
    string BrandName,
    string SerialNo
);
