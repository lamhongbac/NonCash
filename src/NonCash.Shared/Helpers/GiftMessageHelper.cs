namespace NonCash.Shared.Helpers;

/// <summary>
/// CR-2026-09-10-30 row 7: the only place where gifting is worded for customers.
/// Reason and delivery codes stay machine-readable in the API contract; every code is
/// translated here into a cause plus the next action the person can take.
/// </summary>
public static class GiftMessageHelper
{
    /// <summary>Badge shown on a voucher that is reserved for a friend who has not answered yet.</summary>
    public const string OnItsWayBadge = "On its way";

    /// <summary>CR-2026-09-10-31: the one label on every gift-send button, chosen by the product owner.</summary>
    public const string SendGiftButtonLabel = "Gửi Quà";

    /// <summary>CR-2026-09-10-31: told to the brand, because a gift conversation is partly private.</summary>
    public const string BrandPrivacyNote =
        "Only the note sent with the gift and the reply it produced are shown here. Private follow-up messages between the two customers stay between them.";

    /// <summary>Short name for one line of a gift conversation.</summary>
    public static string KindLabel(string? kind) => kind switch
    {
        "GiftNote" => "Sent with the gift",
        "ThankYou" => "Thank-you",
        "DeclineReason" => "Decline reason",
        "FollowUp" => "Message",
        _ => string.Empty
    };

    public static bool IsWaiting(string? status) => status == "PendingAcceptance";

    public static string StatusLabel(string? status) => status switch
    {
        "PendingAcceptance" => "Waiting",
        "Accepted" => "Accepted",
        "Rejected" => "Declined",
        "Expired" => "Expired",
        "Cancelled" => "Cancelled by you",
        _ => status ?? "Unknown"
    };

    /// <summary>Short label for how the gift reached its recipient.</summary>
    public static string DeliveryLabel(string? deliveryStatus) => deliveryStatus switch
    {
        "Emailed" => "Accept link emailed",
        "HoldingNoEmail" => "Held — no email on file",
        "HoldingNotRegistered" => "Held — not on NonCash yet",
        _ => deliveryStatus ?? "Held"
    };

    public static string DescribeSkip(string phone, string? code) => code switch
    {
        "InvalidPhoneNumber" => $"{phone}: that phone number doesn't look right. Check the digits, then send the gift again.",
        "TransferAlreadyPending" => $"{phone}: this voucher is already on its way to someone. Wait for their answer, or cancel it under \"Gifts you sent\" first.",
        "SelfTransferNotAllowed" => $"{phone}: that is your own number. Enter your friend's phone number to send them a gift.",
        "NotTransferable" => $"{phone}: this voucher cannot be gifted in its current state. Only unused vouchers that have not expired can be sent.",
        "NotOwned" => $"{phone}: this voucher is no longer in your wallet, so it cannot be sent. Refresh My Vouchers and try again.",
        "VoucherNotFound" => $"{phone}: we could not find that voucher any more. Refresh My Vouchers and try again.",
        "RecipientNotFound" => $"{phone}: we could not prepare a wallet for that number. Check the phone number and try again.",
        "RecipientBrandBlocked" => $"{phone}: the brand has blocked this customer, so the gift cannot be sent. Ask the brand for help.",
        "RecipientBlacklisted" => $"{phone}: gifts cannot be sent to that number. Contact support if you believe this is a mistake.",
        _ => $"{phone}: the gift was not sent ({code ?? "unknown reason"}). Refresh My Vouchers and try again."
    };
}
