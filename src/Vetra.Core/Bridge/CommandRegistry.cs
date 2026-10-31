namespace Vetra.Bridge;

/// <summary>
/// Thread-safe registry of explicitly allowlisted commands.
/// </summary>
public sealed class CommandRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CommandHandler> _handlers = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a command handler.
    /// </summary>
    /// <exception cref="VetraException">Thrown when the name is invalid or already registered.</exception>
    internal void Register(string name, CommandHandler handler)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            if (_handlers.ContainsKey(name))
            {
                throw new VetraException(
                    VetraErrorCodes.DuplicateCommand,
                    $"A command named '{name}' is already registered.");
            }

            _handlers[name] = handler;
        }
    }

    /// <summary>
    /// Attempts to get a registered handler.
    /// </summary>
    internal bool TryGet(string name, out CommandHandler? handler)
    {
        lock (_gate)
        {
            return _handlers.TryGetValue(name, out handler);
        }
    }

    /// <summary>
    /// Unregisters a command.
    /// </summary>
    public bool Unregister(string name)
    {
        lock (_gate)
        {
            return _handlers.Remove(name);
        }
    }

    /// <summary>
    /// Returns whether a command is registered.
    /// </summary>
    public bool IsRegistered(string name)
    {
        lock (_gate)
        {
            return _handlers.ContainsKey(name);
        }
    }

    /// <summary>
    /// Gets the registered command names.
    /// </summary>
    public IReadOnlyCollection<string> GetNames()
    {
        lock (_gate)
        {
            return _handlers.Keys.ToArray();
        }
    }

    /// <summary>
    /// Removes all registered commands.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _handlers.Clear();
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new VetraException(
                VetraErrorCodes.InvalidArguments,
                "Command name must be a non-empty string.");
        }
    }
}
