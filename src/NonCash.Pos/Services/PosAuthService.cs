using System.Net.Http.Json;
using NonCash.Pos.Models;

namespace NonCash.Pos.Services;

/// <summary>
/// App-shell authentication: calls /api/v1/auth/staff-login (JWT-based), manages the
/// shift session in memory. This is separate from the POS integration contract — the
/// POS endpoints use X-API-Key, not JWT.
/// </summary>
public class PosAuthService
{
    private readonly HttpClient _http;
    private readonly ProvisioningService _provisioning;

    public PosAuthService(HttpClient http, ProvisioningService provisioning)
    {
        _http = http;
        _provisioning = provisioning;
    }

    /// <summary>Current shift session. Null when not logged in.</summary>
    public ShiftSession? CurrentShift { get; private set; }

    public bool IsLoggedIn => CurrentShift is { IsExpired: false };

    /// <summary>
    /// Authenticate staff and start a new shift. The JWT token's expiry defines the
    /// shift length (shift-length token).
    /// </summary>
    public async Task<(bool Success, string? Error)> LoginAsync(string username, string storeCode, string password, string? posNo = null)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/v1/auth/staff-login",
                new StaffLoginApiRequest(username, storeCode, password));

            if (!response.IsSuccessStatusCode)
            {
                return (false, "Invalid credentials. Please check your username, store code, and password.");
            }

            var result = await response.Content.ReadFromJsonAsync<StaffLoginApiResponse>();
            if (result is null)
                return (false, "Server returned an unexpected response.");

            var outlet = await _provisioning.GetOutletAsync();
            CurrentShift = new ShiftSession
            {
                StaffUserId = result.User.UserId,
                StaffName = result.User.FullName,
                OutletId = result.OutletId,
                OutletName = !string.IsNullOrWhiteSpace(result.OutletName) ? result.OutletName : (outlet?.StoreCode ?? storeCode),
                StoreCode = storeCode,
                JwtToken = result.Token,
                ShiftStart = DateTime.UtcNow,
                TokenExpiry = result.ExpiresAt,
                PosNo = posNo,
                RedemptionMode = string.IsNullOrWhiteSpace(result.RedemptionMode)
                    ? ShiftSession.OneClickMode
                    : result.RedemptionMode
            };

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Connection error: {ex.Message}");
        }
    }

    /// <summary>End the current shift explicitly (log out).</summary>
    public void EndShift()
    {
        CurrentShift = null;
    }

    /// <summary>Record a completed transaction in the current shift.</summary>
    public void RecordTransaction(string billNumber, string serialNo, decimal amountUsed, string status)
    {
        CurrentShift?.Transactions.Add(new ShiftTransaction
        {
            Timestamp = DateTime.UtcNow,
            BillNumber = billNumber,
            VoucherSerialNo = serialNo,
            AmountUsed = amountUsed,
            Status = status
        });
    }
}
