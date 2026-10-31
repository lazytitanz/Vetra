namespace Vetra;

/// <summary>
/// Well-known Vetra error codes returned to JavaScript and thrown from .NET APIs.
/// </summary>
public static class VetraErrorCodes
{
    /// <summary>The requested command is not registered.</summary>
    public const string UnknownCommand = "unknown_command";

    /// <summary>A command with the same name is already registered.</summary>
    public const string DuplicateCommand = "duplicate_command";

    /// <summary>Arguments could not be validated or deserialized.</summary>
    public const string InvalidArguments = "invalid_arguments";

    /// <summary>The message envelope was malformed.</summary>
    public const string MalformedMessage = "malformed_message";

    /// <summary>The invoke timed out.</summary>
    public const string Timeout = "timeout";

    /// <summary>The operation was cancelled.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>The bridge or host has been disposed.</summary>
    public const string Disposed = "disposed";

    /// <summary>The requested native capability is disabled.</summary>
    public const string CapabilityDisabled = "capability_disabled";

    /// <summary>An unexpected error occurred while executing a command.</summary>
    public const string CommandFailed = "command_failed";

    /// <summary>The host is not initialized.</summary>
    public const string NotInitialized = "not_initialized";

    /// <summary>Initialization failed.</summary>
    public const string InitializationFailed = "initialization_failed";

    /// <summary>The WebView2 Runtime is missing or incompatible.</summary>
    public const string WebView2Unavailable = "webview2_unavailable";

    /// <summary>Too many pending requests.</summary>
    public const string TooManyRequests = "too_many_requests";

    /// <summary>The message origin is not trusted.</summary>
    public const string UntrustedOrigin = "untrusted_origin";

    /// <summary>A storage operation failed.</summary>
    public const string StorageError = "storage_error";

    /// <summary>A notification could not be shown.</summary>
    public const string NotificationFailed = "notification_failed";
}
