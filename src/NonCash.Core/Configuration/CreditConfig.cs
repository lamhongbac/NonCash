namespace NonCash.Core.Configuration;

/// <summary>
/// Prepaid credit billing configuration (Epic 9).
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
}
