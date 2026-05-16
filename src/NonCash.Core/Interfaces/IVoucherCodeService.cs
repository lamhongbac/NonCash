namespace NonCash.Core.Interfaces;

public interface IVoucherCodeService
{
    /// <summary>
    /// Generates a short-lived signed token for the given voucher detail.
    /// The token contains {vid, iat, exp} signed with HMAC-SHA256 using the detail's secret.
    /// </summary>
    string GenerateCode(Guid voucherDetailId, string secretKey, int validitySeconds = 120);

    /// <summary>
    /// Validates a voucher code token. Returns the voucher detail ID if valid, null if invalid/expired.
    /// </summary>
    Guid? ValidateCode(string code, string secretKey);

    /// <summary>
    /// Generates a random secret key for a voucher detail.
    /// </summary>
    string GenerateSecretKey();

    /// <summary>
    /// Attempts to extract the voucher detail ID from the payload of a code WITHOUT
    /// signature validation. Used by callers that need to look up the secret first.
    /// Returns false if the code is malformed.
    /// </summary>
    bool TryExtractVoucherId(string code, out Guid voucherId);
}
