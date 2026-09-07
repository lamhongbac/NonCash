using Blazored.LocalStorage;
using NonCash.Pos.Models;

namespace NonCash.Pos.Services;

/// <summary>
/// Manages the one-time terminal provisioning: the outlet API key is entered once and
/// stored in localStorage. Like a real POS terminal that belongs to the store — staff
/// turnover never kills the till.
/// </summary>
public class ProvisioningService
{
    private const string StorageKey = "pos_provisioned_outlet";
    private readonly ILocalStorageService _storage;
    private ProvisionedOutlet? _cached;

    public ProvisioningService(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public async Task<ProvisionedOutlet?> GetOutletAsync()
    {
        if (_cached is not null) return _cached;
        _cached = await _storage.GetItemAsync<ProvisionedOutlet>(StorageKey);
        return _cached;
    }

    public async Task<bool> IsProvisionedAsync()
    {
        return await GetOutletAsync() is not null;
    }

    public async Task SaveOutletAsync(ProvisionedOutlet outlet)
    {
        _cached = outlet;
        await _storage.SetItemAsync(StorageKey, outlet);
    }

    public async Task ClearAsync()
    {
        _cached = null;
        await _storage.RemoveItemAsync(StorageKey);
    }
}
