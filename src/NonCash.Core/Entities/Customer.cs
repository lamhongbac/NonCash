namespace NonCash.Core.Entities;

public enum CustomerStatus
{
    Active,
    Blacklisted
}

public class Customer : BaseEntity
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    public static string NormalizePhoneNumber(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Normalizes an email for storage: trimmed and lowercased so uniqueness
    /// checks are case-insensitive. Returns null for blank input (email is optional).
    /// </summary>
    public static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// CR-2026-09-06-11 fill-empty-only: placeholder rows (created by transfer/gifting
    /// before the recipient registers) carry the phone number as FullName. A brand
    /// write may populate such placeholder/empty fields but must never overwrite a
    /// real value — corrections go through the platform admin (CR-2026-09-07-14).
    /// </summary>
    public bool HasPlaceholderName() => string.IsNullOrWhiteSpace(FullName) || FullName == PhoneNumber;
}
