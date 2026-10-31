namespace Vetra.Storage;

/// <summary>
/// Lightweight persistent key/value storage for application preferences.
/// </summary>
public interface IVetraStorage
{
    /// <summary>
    /// Gets a value, or <paramref name="defaultValue"/> when missing.
    /// </summary>
    Task<T?> GetAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a JSON-compatible value.
    /// </summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a key.
    /// </summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a key exists.
    /// </summary>
    Task<bool> ContainsKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all stored values.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
