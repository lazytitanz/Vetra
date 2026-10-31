namespace Vetra;

/// <summary>
/// Represents an error originating from the Vetra framework.
/// </summary>
public sealed class VetraException : Exception
{
    /// <summary>
    /// Gets a machine-readable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets optional structured error details.
    /// </summary>
    public object? Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VetraException"/> class.
    /// </summary>
    public VetraException(string code, string message, object? details = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Details = details;
    }
}
