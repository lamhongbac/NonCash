using MudBlazor;

namespace NonCash.Web.Components.Shared;

/// <summary>
/// Shared lifecycle-status → MudBlazor color mapping for voucher status chips
/// (plan voucher ledger, batch run detail, customer voucher history).
/// </summary>
public static class VoucherStatusDisplay
{
    public static Color ChipColor(string? status) => status switch
    {
        "Redeemed" => Color.Success,
        "Redeeming" => Color.Warning,
        "Distributed" => Color.Info,
        "Expired" => Color.Error,
        _ => Color.Default, // InStock / unknown
    };
}
