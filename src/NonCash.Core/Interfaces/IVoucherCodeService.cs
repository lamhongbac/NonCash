namespace NonCash.Core.Interfaces;

/// <summary>Why a presented voucher code token did not validate.</summary>
public enum VoucherCodeCheck
{
    /// <summary>Signature verifies and the token is within its validity window.</summary>
    Valid,

    /// <summary>Not a token at all (e.g. a pasted serial number) or an undecodable payload.</summary>
    Malformed,

    /// <summary>Genuine token whose validity window has passed; the customer must refresh it.</summary>
    Expired,

    /// <summary>Token shape is fine but the HMAC does not match the stored secret: counterfeit.</summary>
    BadSignature
}

public interface IVoucherCodeService
{
    /// <summary>
    /// Generates a short-lived signed token for the given voucher detail.
    /// The token contains {vid, iat, exp} signed with HMAC-SHA256 using the detail's secret.
    /// <paramref name="validitySeconds"/> overrides the configured default
    /// (<see cref="VoucherCodeOptions.CodeValiditySeconds"/>) for this one token; pass null to
    /// use the configured value.
    /// </summary>
    string GenerateCode(Guid voucherDetailId, string secretKey, int? validitySeconds = null);

    /// <summary>
    /// Classifies a presented code against the detail's secret and outputs the voucher
    /// detail ID carried in the payload (Guid.Empty unless <see cref="VoucherCodeCheck.Valid"/>).
    /// </summary>
    VoucherCodeCheck CheckCode(string code, string secretKey, out Guid voucherId);

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
