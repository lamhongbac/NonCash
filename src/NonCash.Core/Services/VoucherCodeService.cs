using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NonCash.Core.Services;

public class VoucherCodeService : IVoucherCodeService
{
    // Payload is serialized with lowercase names ({vid, iat, exp}); deserialization
    // must be case-insensitive to map them onto VoucherCodePayload properties.
    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string GenerateCode(Guid voucherDetailId, string secretKey, int validitySeconds = 120)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new
        {
            vid = voucherDetailId.ToString(),
            iat = now,
            exp = now + validitySeconds
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);

        var signature = ComputeHmac(payloadBase64, secretKey);
        var signatureBase64 = Convert.ToBase64String(signature);

        return $"{payloadBase64}.{signatureBase64}";
    }

    public VoucherCodeCheck CheckCode(string code, string secretKey, out Guid voucherId)
    {
        voucherId = Guid.Empty;
        try
        {
            var parts = code.Split('.');
            if (parts.Length != 2) return VoucherCodeCheck.Malformed;

            var payloadBase64 = parts[0];

            // Signature first: a tampered token must never be reported as merely expired.
            var expectedSignature = ComputeHmac(payloadBase64, secretKey);
            var actualSignature = Convert.FromBase64String(parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
                return VoucherCodeCheck.BadSignature;

            var payloadBytes = Convert.FromBase64String(payloadBase64);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            var payload = JsonSerializer.Deserialize<VoucherCodePayload>(payloadJson, PayloadJsonOptions);
            if (payload == null) return VoucherCodeCheck.Malformed;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now > payload.Exp) return VoucherCodeCheck.Expired;

            return Guid.TryParse(payload.Vid, out voucherId)
                ? VoucherCodeCheck.Valid
                : VoucherCodeCheck.Malformed;
        }
        catch
        {
            return VoucherCodeCheck.Malformed;
        }
    }

    public string GenerateSecretKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public bool TryExtractVoucherId(string code, out Guid voucherId)
    {
        voucherId = Guid.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            var parts = code.Split('.');
            if (parts.Length != 2) return false;

            var payloadBytes = Convert.FromBase64String(parts[0]);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            var payload = JsonSerializer.Deserialize<VoucherCodePayload>(payloadJson, PayloadJsonOptions);
            if (payload == null) return false;

            return Guid.TryParse(payload.Vid, out voucherId);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ComputeHmac(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private class VoucherCodePayload
    {
        public string Vid { get; set; } = "";
        public long Iat { get; set; }
        public long Exp { get; set; }
    }
}
