using System.Collections.Concurrent;
using System.Text.Json;
using Vetra.Protocol;

namespace Vetra.Bridge;

/// <summary>
/// Default host-agnostic command bridge implementation.
/// </summary>
public sealed class VetraBridge : IVetraBridge
{
    private readonly IVetraMessageTransport _transport;
    private readonly CommandRegistry _registry = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new(StringComparer.Ordinal);
    private readonly IVetraLogger _logger;
    private readonly TimeSpan? _defaultTimeout;
    private readonly int _maxPendingRequests;
    private readonly object _disposeGate = new();
    private int _pendingCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VetraBridge"/> class.
    /// </summary>
    public VetraBridge(
        IVetraMessageTransport transport,
        TimeSpan? defaultInvokeTimeout = null,
        int maxPendingRequests = 100,
        IVetraLogger? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _defaultTimeout = defaultInvokeTimeout;
        _maxPendingRequests = maxPendingRequests < 1 ? 1 : maxPendingRequests;
        _logger = logger ?? NullVetraLogger.Instance;
    }

    /// <inheritdoc />
    public bool IsDisposed => _disposed;

    /// <inheritdoc />
    public void Register(string name, Func<object?> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.Create(handler));
    }

    /// <inheritdoc />
    public void RegisterIgnoringArgs(string name, Func<object?> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.CreateIgnoringArgs(handler));
    }

    /// <inheritdoc />
    public void Register<T1>(string name, Func<T1, object?> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.Create(handler));
    }

    /// <inheritdoc />
    public void Register<T1, T2>(string name, Func<T1, T2, object?> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.Create(handler));
    }

    /// <inheritdoc />
    public void Register<T1, T2, T3>(string name, Func<T1, T2, T3, object?> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.Create(handler));
    }

    /// <inheritdoc />
    public void RegisterAsync(string name, Func<CancellationToken, Task<object?>> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.CreateAsync(handler));
    }

    /// <inheritdoc />
    public void RegisterAsync<T1>(string name, Func<T1, CancellationToken, Task<object?>> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.CreateAsync(handler));
    }

    /// <inheritdoc />
    public void RegisterAsync<T1, T2>(string name, Func<T1, T2, CancellationToken, Task<object?>> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.CreateAsync(handler));
    }

    /// <inheritdoc />
    public void RegisterAsync<T1, T2, T3>(string name, Func<T1, T2, T3, CancellationToken, Task<object?>> handler)
    {
        ThrowIfDisposed();
        _registry.Register(name, CommandHandler.CreateAsync(handler));
    }

    /// <inheritdoc />
    public bool Unregister(string name)
    {
        ThrowIfDisposed();
        return _registry.Unregister(name);
    }

    /// <inheritdoc />
    public bool IsRegistered(string name) => _registry.IsRegistered(name);

    /// <inheritdoc />
    public async Task HandleIncomingAsync(string json, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        VetraMessage? message;
        try
        {
            message = VetraJson.Deserialize<VetraMessage>(json);
        }
        catch (Exception ex)
        {
            _logger.Log(VetraLogLevel.Warning, $"Malformed bridge message: {ex.Message}");
            return;
        }

        if (message is null || string.IsNullOrWhiteSpace(message.Type))
        {
            _logger.Log(VetraLogLevel.Warning, "Ignoring empty or typeless bridge message.");
            return;
        }

        if (!string.Equals(message.Type, VetraMessageType.Invoke, StringComparison.Ordinal))
        {
            _logger.Log(VetraLogLevel.Debug, $"Ignoring non-invoke bridge message type '{message.Type}'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(message.Id) || string.IsNullOrWhiteSpace(message.Name))
        {
            _logger.Log(VetraLogLevel.Warning, "Ignoring invoke without id or name.");
            return;
        }

        await ExecuteInvokeAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EmitAsync(string eventName, object? data = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new VetraException(VetraErrorCodes.InvalidArguments, "Event name must be a non-empty string.");
        }

        var message = new VetraMessage
        {
            Type = VetraMessageType.Event,
            Name = eventName,
            Data = data
        };

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void CancelPending()
    {
        foreach (var pair in _pending)
        {
            try
            {
                pair.Value.Cancel();
            }
            catch
            {
                // ignored
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
        }

        CancelPending();
        foreach (var pair in _pending)
        {
            pair.Value.Dispose();
        }

        _pending.Clear();
        _registry.Clear();
        return ValueTask.CompletedTask;
    }

    private async Task ExecuteInvokeAsync(VetraMessage message, CancellationToken cancellationToken)
    {
        var id = message.Id!;
        var name = message.Name!;

        if (Interlocked.Increment(ref _pendingCount) > _maxPendingRequests)
        {
            Interlocked.Decrement(ref _pendingCount);
            await SendErrorAsync(id, VetraErrorCodes.TooManyRequests,
                $"Too many pending requests (limit {_maxPendingRequests}).", cancellationToken).ConfigureAwait(false);
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_defaultTimeout is { } timeout)
        {
            linked.CancelAfter(timeout);
        }

        if (!_pending.TryAdd(id, linked))
        {
            Interlocked.Decrement(ref _pendingCount);
            await SendErrorAsync(id, VetraErrorCodes.MalformedMessage,
                $"Duplicate or invalid request id '{id}'.", cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            if (!_registry.TryGet(name, out var handler) || handler is null)
            {
                await SendErrorAsync(id, VetraErrorCodes.UnknownCommand,
                    $"Unknown command '{name}'.", linked.Token).ConfigureAwait(false);
                return;
            }

            object? result;
            try
            {
                result = await handler.ExecuteAsync(message.Args, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested && !cancellationToken.IsCancellationRequested && _defaultTimeout is not null)
            {
                await SendErrorAsync(id, VetraErrorCodes.Timeout,
                    $"Command '{name}' timed out.", CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                await SendErrorAsync(id, VetraErrorCodes.Cancelled,
                    $"Command '{name}' was cancelled.", CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (VetraException vex)
            {
                await SendErrorAsync(id, vex.Code, vex.Message, CancellationToken.None, vex.Details).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                _logger.Log(VetraLogLevel.Error, $"Command '{name}' failed: {ex}");
                await SendErrorAsync(id, VetraErrorCodes.CommandFailed,
                    ex.Message, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await SendAsync(new VetraMessage
            {
                Type = VetraMessageType.Result,
                Id = id,
                Data = result
            }, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out var cts);
            cts?.Dispose();
            Interlocked.Decrement(ref _pendingCount);
        }
    }

    private Task SendErrorAsync(
        string id,
        string code,
        string message,
        CancellationToken cancellationToken,
        object? details = null)
    {
        return SendAsync(new VetraMessage
        {
            Type = VetraMessageType.Error,
            Id = id,
            Error = new VetraErrorPayload
            {
                Code = code,
                Message = message,
                Details = details
            }
        }, cancellationToken);
    }

    private async Task SendAsync(VetraMessage message, CancellationToken cancellationToken)
    {
        var json = VetraJson.Serialize(message);
        await _transport.SendAsync(json, cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new VetraException(VetraErrorCodes.Disposed, "The Vetra bridge has been disposed.");
        }
    }
}
