namespace NonCash.Core.Services;

/// <summary>Configuration for dynamically minted voucher code tokens (configuration section "VoucherCode").</summary>
public class VoucherCodeOptions
{
    public const string SectionName = "VoucherCode";

    /// <summary>
    /// How long a minted code token stays valid, in seconds. Read at service construction and
    /// clamped to <see cref="MinCodeValiditySeconds"/>–<see cref="MaxCodeValiditySeconds"/> so a
    /// config mistake can neither mint unusable sub-second codes nor long-lived bearer tokens.
    /// </summary>
    public int CodeValiditySeconds { get; set; } = 120;

    public const int MinCodeValiditySeconds = 15;

    public const int MaxCodeValiditySeconds = 300;

    public int ClampedCodeValiditySeconds =>
        Math.Clamp(CodeValiditySeconds, MinCodeValiditySeconds, MaxCodeValiditySeconds);
}
