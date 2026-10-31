using System.Text.Json;
using Vetra.Protocol;

namespace Vetra.Storage;

/// <summary>
/// File-backed JSON preference store with atomic writes and a process-wide file lock.
/// </summary>
public sealed class JsonFileStorage : IVetraStorage
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IVetraLogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileStorage"/> class.
    /// </summary>
    public JsonFileStorage(string filePath, IVetraLogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Storage file path is required.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
        _logger = logger ?? NullVetraLogger.Instance;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!data.TryGetValue(key, out var element))
            {
                return defaultValue;
            }

            try
            {
                return VetraJson.Deserialize<T>(element) ?? defaultValue;
            }
            catch (Exception ex)
            {
                throw new VetraException(
                    VetraErrorCodes.StorageError,
                    $"Stored value for '{key}' could not be deserialized.",
                    innerException: ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await ReadAsync(cancellationToken).ConfigureAwait(false);
            data[key] = JsonSerializer.SerializeToElement(value, VetraJson.Options);
            await WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var removed = data.Remove(key);
            if (removed)
            {
                await WriteAsync(data, cancellationToken).ConfigureAwait(false);
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ContainsKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return data.ContainsKey(key);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAsync(new Dictionary<string, JsonElement>(StringComparer.Ordinal), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, JsonElement>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        try
        {
            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            if (stream.Length == 0)
            {
                return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            }

            var data = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(
                stream,
                VetraJson.Options,
                cancellationToken).ConfigureAwait(false);

            return data is null
                ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                : new Dictionary<string, JsonElement>(data, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.Log(VetraLogLevel.Warning, $"Storage file '{_filePath}' is corrupt and will be reset: {ex.Message}");
            TryBackupCorruptFile();
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }
    }

    private async Task WriteAsync(Dictionary<string, JsonElement> data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var tempPath = _filePath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, data, VetraJson.Options, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Copy(tempPath, _filePath, overwrite: true);
            File.Delete(tempPath);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // ignored
            }

            throw new VetraException(
                VetraErrorCodes.StorageError,
                $"Failed to write storage file '{_filePath}'.",
                innerException: ex);
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var backup = _filePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            File.Copy(_filePath, backup, overwrite: true);
        }
        catch
        {
            // ignored
        }
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new VetraException(VetraErrorCodes.InvalidArguments, "Storage key must be a non-empty string.");
        }
    }
}
