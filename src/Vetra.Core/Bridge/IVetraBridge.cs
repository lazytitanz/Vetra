using System.Text.Json;

namespace Vetra.Bridge;

/// <summary>
/// Registers commands, handles incoming invokes, and emits events to JavaScript.
/// </summary>
public interface IVetraBridge : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the bridge is disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Registers a synchronous command with no parameters.
    /// </summary>
    void Register(string name, Func<object?> handler);

    /// <summary>
    /// Registers a synchronous command that ignores inbound argument shape.
    /// Intended for capability-disabled stubs.
    /// </summary>
    void RegisterIgnoringArgs(string name, Func<object?> handler);

    /// <summary>
    /// Registers a synchronous command with one parameter.
    /// </summary>
    void Register<T1>(string name, Func<T1, object?> handler);

    /// <summary>
    /// Registers a synchronous command with two parameters.
    /// </summary>
    void Register<T1, T2>(string name, Func<T1, T2, object?> handler);

    /// <summary>
    /// Registers a synchronous command with three parameters.
    /// </summary>
    void Register<T1, T2, T3>(string name, Func<T1, T2, T3, object?> handler);

    /// <summary>
    /// Registers an asynchronous command with no parameters.
    /// </summary>
    void RegisterAsync(string name, Func<CancellationToken, Task<object?>> handler);

    /// <summary>
    /// Registers an asynchronous command with one parameter.
    /// </summary>
    void RegisterAsync<T1>(string name, Func<T1, CancellationToken, Task<object?>> handler);

    /// <summary>
    /// Registers an asynchronous command with two parameters.
    /// </summary>
    void RegisterAsync<T1, T2>(string name, Func<T1, T2, CancellationToken, Task<object?>> handler);

    /// <summary>
    /// Registers an asynchronous command with three parameters.
    /// </summary>
    void RegisterAsync<T1, T2, T3>(string name, Func<T1, T2, T3, CancellationToken, Task<object?>> handler);

    /// <summary>
    /// Unregisters a previously registered command.
    /// </summary>
    bool Unregister(string name);

    /// <summary>
    /// Returns whether a command is registered.
    /// </summary>
    bool IsRegistered(string name);

    /// <summary>
    /// Handles an inbound JSON message from the frontend.
    /// </summary>
    Task HandleIncomingAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits an event to JavaScript listeners.
    /// </summary>
    Task EmitAsync(string eventName, object? data = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels all pending incoming invokes.
    /// </summary>
    void CancelPending();
}
