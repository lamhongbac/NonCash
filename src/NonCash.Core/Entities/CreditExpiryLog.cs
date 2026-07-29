namespace NonCash.Core.Entities;

/// <summary>
/// Audit record written when the expiry job zeroes out a batch past its ExpiresAt (Epic 10).
/// </summary>
public class CreditExpiryLog : BaseEntity
{
    public Guid BatchId { get; set; }

    public Guid BrandId { get; set; }

    /// <summary>Credits forfeited (the batch's RemainingAmount at expiry time).</summary>
    public int ExpiredCredits { get; set; }

    /// <summary>When the expiry was executed (UTC).</summary>
    public DateTime ExpiredAt { get; set; }

    // Navigation
    public CreditBatch? Batch { get; set; }
    public Brand? Brand { get; set; }
}
