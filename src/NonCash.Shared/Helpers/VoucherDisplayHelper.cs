using System.Globalization;
using NonCash.Shared.Enums;

namespace NonCash.Shared.Helpers;

/// <summary>
/// Centralized display formatting for voucher values, status badges, and expiry.
/// Used by Blazor components, API responses, and loyalty-app payloads.
/// </summary>
public static class VoucherDisplayHelper
{
    /// <summary>
    /// Formats a voucher's face value according to its type and culture.
    /// Examples: "200.000 ₫" (Value), "20% OFF" (Percentage).
    /// </summary>
    public static string FormatValue(decimal faceValue, VoucherValueType type, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        return type switch
        {
            VoucherValueType.Percentage => $"{faceValue:0}% OFF",
            VoucherValueType.Value => $"{faceValue.ToString("N0", culture)} ₫",
            _ => faceValue.ToString("N0", culture)
        };
    }

    /// <summary>
    /// Overload accepting the Core entity enum (NonCash.Core.Entities.VoucherValueType)
    /// to avoid requiring callers to map between the duplicate enum definitions.
    /// </summary>
    public static string FormatValue(decimal faceValue, object valueTypeEnum, CultureInfo? culture = null)
    {
        var name = valueTypeEnum?.ToString() ?? "Value";
        var mapped = name.Contains("Percentage", StringComparison.OrdinalIgnoreCase)
            ? VoucherValueType.Percentage
            : VoucherValueType.Value;
        return FormatValue(faceValue, mapped, culture);
    }

    /// <summary>
    /// Computes a human-readable status badge for a voucher.
    /// </summary>
    public static string ComputeStatusBadge(VoucherStatus usageStatus, DateTime expiryDate)
    {
        var now = DateTime.UtcNow;

        return usageStatus switch
        {
            VoucherStatus.Complete => "Used",
            VoucherStatus.InUse => "In Use",
            VoucherStatus.Pending when now > expiryDate => "Expired",
            VoucherStatus.Pending when (expiryDate - now).TotalDays <= 3 => "Expiring Soon",
            VoucherStatus.Pending => "Active",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Overload accepting int (raw enum value) for convenience from EF entities.
    /// </summary>
    public static string ComputeStatusBadge(int usageStatusInt, DateTime expiryDate)
    {
        var status = (VoucherStatus)usageStatusInt;
        return ComputeStatusBadge(status, expiryDate);
    }

    /// <summary>
    /// Returns a human-friendly expiry display string.
    /// Examples: "5 days left", "Expires today", "Expired".
    /// </summary>
    public static string ComputeExpiryDisplay(DateTime expiryDate)
    {
        var now = DateTime.UtcNow;
        var daysLeft = (expiryDate.Date - now.Date).Days;

        if (daysLeft < 0)
            return "Expired";
        if (daysLeft == 0)
            return "Expires today";
        if (daysLeft == 1)
            return "1 day left";
        return $"{daysLeft} days left";
    }
}
