using System.Net;
using System.Net.Http.Json;
using NonCash.Pos.Models;

namespace NonCash.Pos.Services;

/// <summary>
/// HTTP client that dogfoods the public POS integration contract. Every call sends
/// the outlet's X-API-Key header, exactly like any third-party POS would.
/// Base address is configured from the API URL (same origin or CORS-enabled).
/// </summary>
public class PosApiClient : IPosApiClient
{
    private readonly HttpClient _http;
    private readonly ProvisioningService _provisioning;

    public PosApiClient(HttpClient http, ProvisioningService provisioning)
    {
        _http = http;
        _provisioning = provisioning;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path)
    {
        var outlet = await _provisioning.GetOutletAsync()
            ?? throw new InvalidOperationException("Terminal not provisioned.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-API-Key", outlet.ApiKey);
        return request;
    }

    /// <summary>
    /// Whether the body is a POS result rather than a middleware error. 409 counts as a result:
    /// AlreadyInUse, LockExpired and AlreadyCompleted are business outcomes the API signals with
    /// 409 and a normal { status, reason } body. 401 and 429 instead carry { error }, which would
    /// deserialize into an all-null result and leave the cashier with a blank screen.
    /// </summary>
    private static bool CarriesPosResult(HttpResponseMessage response) =>
        response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict;

    private static string RejectionReason(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.TooManyRequests => PosFailureReasons.RateLimited,
        HttpStatusCode.Unauthorized => PosFailureReasons.TerminalRejected,
        _ => PosFailureReasons.ServiceRejected
    };

    public async Task<PosVerifyResponse?> VerifyAsync(string voucherCode, Guid outletId)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, "api/v1/pos/verify");
        request.Content = JsonContent.Create(new PosVerifyRequest(voucherCode, outletId));
        var response = await _http.SendAsync(request);
        return CarriesPosResult(response)
            ? await response.Content.ReadFromJsonAsync<PosVerifyResponse>()
            : new PosVerifyResponse("Invalid", RejectionReason(response), null);
    }

    public async Task<PosLockResponse?> LockAsync(string voucherCode, Guid outletId, string billNumber, string? posNo = null, string? operatorId = null)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, "api/v1/pos/lock");
        request.Content = JsonContent.Create(new PosLockRequest(voucherCode, outletId, billNumber, posNo, operatorId));
        var response = await _http.SendAsync(request);
        return CarriesPosResult(response)
            ? await response.Content.ReadFromJsonAsync<PosLockResponse>()
            : new PosLockResponse("Invalid", RejectionReason(response), null, null);
    }

    public async Task<PosCommitResponse?> CommitAsync(Guid lockId, string transactionId, decimal amountUsed, string? posNo = null, string? operatorId = null)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, "api/v1/pos/commit");
        request.Content = JsonContent.Create(new PosCommitRequest(lockId, transactionId, amountUsed, posNo, operatorId));
        var response = await _http.SendAsync(request);
        return CarriesPosResult(response)
            ? await response.Content.ReadFromJsonAsync<PosCommitResponse>()
            : new PosCommitResponse("Invalid", RejectionReason(response), null);
    }

    public async Task<PosRollbackResponse?> RollbackAsync(Guid lockId)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, "api/v1/pos/rollback");
        request.Content = JsonContent.Create(new PosRollbackRequest(lockId));
        var response = await _http.SendAsync(request);
        return CarriesPosResult(response)
            ? await response.Content.ReadFromJsonAsync<PosRollbackResponse>()
            : new PosRollbackResponse("Invalid", RejectionReason(response), null);
    }
}
