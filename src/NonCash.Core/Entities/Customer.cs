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
}
