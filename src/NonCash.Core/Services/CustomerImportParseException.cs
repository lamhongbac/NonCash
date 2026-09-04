namespace NonCash.Core.Services;

/// <summary>
/// Thrown when an uploaded customer import file cannot be parsed at all
/// (wrong format, unreadable workbook, unrecognizable headers...).
/// The message is user-facing and should point at the Excel template.
/// </summary>
public class CustomerImportParseException : Exception
{
    public CustomerImportParseException(string message) : base(message)
    {
    }

    public CustomerImportParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
