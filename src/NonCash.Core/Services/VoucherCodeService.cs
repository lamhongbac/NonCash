using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NonCash.Core.Services;

public class VoucherCodeService : IVoucherCodeService
{
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

    public Guid? ValidateCode(string code, string secretKey)
    {
        try
        {
            var parts = code.Split('.');
            if (parts.Length != 2) return null;

            var payloadBase64 = parts[0];
            var signatureBase64 = parts[1];

            // Verify signature
            var expectedSignature = ComputeHmac(payloadBase64, secretKey);
            var actualSignature = Convert.FromBase64String(signatureBase64);

            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
                return null;

            // Decode payload
            var payloadBytes = Convert.FromBase64String(payloadBase64);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            var payload = JsonSerializer.Deserialize<VoucherCodePayload>(payloadJson);

            if (payload == null) return null;

            // Check expiry
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now > payload.Exp) return null;

            if (Guid.TryParse(payload.Vid, out var vid))
                return vid;

            return null;
        }
        catch
        {
            return null;
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
            var payload = JsonSerializer.Deserialize<VoucherCodePayload>(payloadJson);
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
