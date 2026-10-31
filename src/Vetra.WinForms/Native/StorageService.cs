using System.Text.Json;
using Vetra.Bridge;
using Vetra.Storage;

namespace Vetra.WinForms.Native;

internal sealed class StorageService
{
    private readonly IVetraStorage _storage;

    public StorageService(IVetraStorage storage)
    {
        _storage = storage;
    }

    public void Register(IVetraBridge bridge)
    {
        bridge.RegisterAsync<string, JsonElement?>("vetra.storage.get", async (key, defaultValue, ct) =>
        {
            if (defaultValue is null || defaultValue.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return await _storage.GetAsync<JsonElement?>(key, null, ct).ConfigureAwait(false);
            }

            var existing = await _storage.GetAsync<JsonElement?>(key, null, ct).ConfigureAwait(false);
            return existing ?? defaultValue;
        });

        bridge.RegisterAsync<string, JsonElement>("vetra.storage.set", async (key, value, ct) =>
        {
            await _storage.SetAsync(key, value, ct).ConfigureAwait(false);
            return null;
        });

        bridge.RegisterAsync<string>("vetra.storage.remove", async (key, ct) =>
            await _storage.RemoveAsync(key, ct).ConfigureAwait(false));

        bridge.RegisterAsync("vetra.storage.clear", async ct =>
        {
            await _storage.ClearAsync(ct).ConfigureAwait(false);
            return null;
        });
    }
}
