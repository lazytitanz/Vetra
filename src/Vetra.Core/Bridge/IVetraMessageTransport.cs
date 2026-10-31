namespace Vetra.Bridge;

/// <summary>
/// Host-agnostic transport used by the command bridge to send messages to JavaScript.
/// </summary>
public interface IVetraMessageTransport
{
    /// <summary>
    /// Sends a JSON message to the frontend.
    /// </summary>
    Task SendAsync(string json, CancellationToken cancellationToken = default);
}
