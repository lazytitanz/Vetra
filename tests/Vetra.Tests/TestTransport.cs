using System.Collections.Concurrent;
using Vetra.Bridge;

namespace Vetra.Tests;

internal sealed class TestTransport : IVetraMessageTransport
{
    private readonly ConcurrentQueue<string> _messages = new();
    private readonly SemaphoreSlim _signal = new(0);

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public Task SendAsync(string json, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(json);
        _signal.Release();
        return Task.CompletedTask;
    }

    public async Task<string> WaitForMessageAsync(TimeSpan timeout)
    {
        if (!await _signal.WaitAsync(timeout).ConfigureAwait(false))
        {
            throw new TimeoutException("Timed out waiting for a transport message.");
        }

        if (!_messages.TryDequeue(out var message))
        {
            throw new InvalidOperationException("Signal received but no message was queued.");
        }

        return message;
    }
}
