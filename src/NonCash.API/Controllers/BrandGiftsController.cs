using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// CR-2026-09-10-30 row 6: read-only gift log for a brand — who sent which voucher to whom,
/// when, and how the gift ended. A BrandManager sees only their own brand; an Admin may filter
/// by brand or see all of them.
/// </summary>
[ApiController]
[Route("api/v1/brand/gifts")]
[Authorize(Roles = "BrandManager,Admin")]
public class BrandGiftsController : ControllerBase
{
    private readonly IVoucherTransferService _transferService;
    private readonly ICurrentUserService _currentUser;

    public BrandGiftsController(IVoucherTransferService transferService, ICurrentUserService currentUser)
    {
        _transferService = transferService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BrandGiftDto>>> GetGifts(
        [FromQuery] Guid? brandId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        Guid? scopeBrandId;
        if (_currentUser.IsInRole("BrandManager"))
        {
            var ownBrandId = _currentUser.GetCurrentBrandId();
            if (ownBrandId == null)
                return Unauthorized(new
                {
                    error = "Invalid user context.",
                    message = "Your account is not linked to a brand, so there are no gifts to show. Ask an administrator to link your account to a brand."
                });

            scopeBrandId = ownBrandId;
        }
        else
        {
            scopeBrandId = brandId; // Admin: optional filter, null means every brand.
        }

        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 50;

        VoucherTransferStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<VoucherTransferStatus>(status, true, out var parsed))
            {
                return BadRequest(new
                {
                    error = "Validation",
                    message = $"'{status}' is not a gift status. Use PendingAcceptance, Accepted, Rejected, Expired or Cancelled."
                });
            }

            statusFilter = parsed;
        }

        var items = await _transferService.GetBrandGiftsAsync(scopeBrandId, statusFilter, page, pageSize, cancellationToken);

        return Ok(items.Select(i => new BrandGiftDto(
            i.TransferId,
            i.VoucherId,
            i.SerialNo,
            i.BrandName,
            i.FaceValue,
            i.ValueType,
            i.SenderName,
            i.SenderPhone,
            i.RecipientName,
            i.RecipientPhone,
            i.Note,
            i.RecipientNote,
            i.RejectReason,
            i.Status.ToString(),
            i.InitiatedAt,
            i.ExpiresAt,
            i.RespondedAt,
            i.MessageCount,
            i.LastMessageAt)));
    }

    /// <summary>CR-2026-09-10-31 row 10: the part of a gift's conversation a brand may read —
    /// the note that travelled with the voucher and the reply it produced. Follow-up messages
    /// between the two members stay private and are never returned here.</summary>
    [HttpGet("{transferId:guid}/messages")]
    public async Task<ActionResult<BrandGiftThreadDto>> GetMessages(
        Guid transferId,
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken = default)
    {
        Guid? scopeBrandId;
        if (_currentUser.IsInRole("BrandManager"))
        {
            var ownBrandId = _currentUser.GetCurrentBrandId();
            if (ownBrandId == null)
                return Unauthorized(new
                {
                    error = "Invalid user context.",
                    message = "Your account is not linked to a brand, so there are no gift messages to show. Ask an administrator to link your account to a brand."
                });

            scopeBrandId = ownBrandId;
        }
        else
        {
            scopeBrandId = brandId;
        }

        var messages = await _transferService.GetBrandMessagesAsync(transferId, scopeBrandId, cancellationToken);

        return Ok(new BrandGiftThreadDto(
            transferId,
            messages.Select(m => new BrandGiftMessageDto(
                m.MessageId,
                m.AuthorDisplayName,
                m.Direction.ToString(),
                m.Kind.ToString(),
                m.Body,
                m.SentAt))));
    }
}

public record BrandGiftDto(
    Guid TransferId,
    Guid VoucherId,
    string SerialNo,
    string BrandName,
    decimal FaceValue,
    string? ValueType,
    string SenderName,
    string SenderPhone,
    string RecipientName,
    string RecipientPhone,
    string? Note,
    string? RecipientNote,
    string? RejectReason,
    string Status,
    DateTime InitiatedAt,
    DateTime ExpiresAt,
    DateTime? RespondedAt,
    int MessageCount = 0,
    DateTime? LastMessageAt = null);

/// <summary>CR-2026-09-10-31: the brand-visible conversation of one gift.</summary>
public record BrandGiftThreadDto(Guid TransferId, IEnumerable<BrandGiftMessageDto> Messages);

public record BrandGiftMessageDto(
    Guid MessageId,
    string AuthorDisplayName,
    string Direction,
    string Kind,
    string Body,
    DateTime SentAt);
