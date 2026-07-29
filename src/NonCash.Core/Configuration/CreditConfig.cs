namespace NonCash.Core.Configuration;

/// <summary>
/// Prepaid credit billing configuration (Epic 9).
/// Since Epic 10 these values are only the fallback when no DB pricing policy matches.
/// </summary>
public class CreditConfig
{
    public const string SectionName = "CreditConfig";

    /// <summary>
    /// Free credits granted to each newly activated brand (the "free period").
    /// </summary>
    public int WelcomeCredits { get; set; } = 500;

    /// <summary>
    /// Remaining-balance percentage threshold for low-balance warnings (reserved for UI).
    /// </summary>
    public int LowBalanceWarningPercent { get; set; } = 20;

    /// <summary>Fallback flat unit price (VND) when no pricing policy is in force.</summary>
    public decimal PricePerCreditVnd { get; set; } = 5000m;

    /// <summary>Fallback months until a purchased batch expires. Null = never.</summary>
    public int? CreditExpiryMonths { get; set; } = 12;

    /// <summary>Fallback months until a welcome-grant batch expires. Null = never.</summary>
    public int? WelcomeCreditExpiryMonths { get; set; } = 12;

    /// <summary>Fallback days before batch expiry to warn the brand.</summary>
    public int? ExpiryWarningDays { get; set; } = 30;

    /// <summary>Fallback approval threshold for Grant/Compensation adjustments.</summary>
    public int? AdjustmentApprovalThreshold { get; set; } = 1000;
}
