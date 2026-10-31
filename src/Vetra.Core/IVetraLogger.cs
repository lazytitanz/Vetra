namespace Vetra;

/// <summary>
/// Minimal logging hook for Vetra. Applications may adapt this to any logging framework.
/// </summary>
public interface IVetraLogger
{
    /// <summary>Writes a diagnostic message.</summary>
    void Log(VetraLogLevel level, string message);
}

/// <summary>
/// Log severity for <see cref="IVetraLogger"/>.
/// </summary>
public enum VetraLogLevel
{
    /// <summary>Detailed diagnostic information.</summary>
    Debug = 0,

    /// <summary>Informational messages.</summary>
    Information = 1,

    /// <summary>Warning conditions.</summary>
    Warning = 2,

    /// <summary>Error conditions.</summary>
    Error = 3
}

/// <summary>
/// A logger that discards all messages.
/// </summary>
public sealed class NullVetraLogger : IVetraLogger
{
    /// <summary>Gets the shared instance.</summary>
    public static NullVetraLogger Instance { get; } = new();

    private NullVetraLogger()
    {
    }

    /// <inheritdoc />
    public void Log(VetraLogLevel level, string message)
    {
    }
}

/// <summary>
/// A logger that forwards messages to a callback.
/// </summary>
public sealed class CallbackVetraLogger : IVetraLogger
{
    private readonly Action<VetraLogLevel, string> _callback;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackVetraLogger"/> class.
    /// </summary>
    public CallbackVetraLogger(Action<VetraLogLevel, string> callback)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <inheritdoc />
    public void Log(VetraLogLevel level, string message) => _callback(level, message);
}
