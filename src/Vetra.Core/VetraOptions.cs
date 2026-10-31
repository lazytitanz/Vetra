namespace Vetra;

/// <summary>
/// Configuration for initializing a Vetra host.
/// </summary>
public class VetraOptions
{
    /// <summary>
    /// Gets or sets the local frontend root folder, relative to the application base directory
    /// or an absolute path. Default is <c>www</c>.
    /// </summary>
    public string WebRoot { get; set; } = "www";

    /// <summary>
    /// Gets or sets the startup page relative to <see cref="WebRoot"/>. Default is <c>index.html</c>.
    /// </summary>
    public string StartPage { get; set; } = "index.html";

    /// <summary>
    /// Gets or sets the virtual host name used to serve local files.
    /// Default is <c>app.vetra.local</c>.
    /// </summary>
    public string VirtualHostName { get; set; } = "app.vetra.local";

    /// <summary>
    /// Gets or sets whether the WebView2 developer tools are enabled.
    /// </summary>
    public bool EnableDevTools { get; set; }

    /// <summary>
    /// Gets or sets an explicit WebView2 user-data folder.
    /// When null, Vetra uses a persistent per-application default.
    /// </summary>
    public string? UserDataFolder { get; set; }

    /// <summary>
    /// Gets or sets whether this host should use an isolated WebView2 profile
    /// instead of the shared per-application profile.
    /// </summary>
    public bool IsolateWebViewProfile { get; set; }

    /// <summary>
    /// Gets or sets the optional isolation key used when <see cref="IsolateWebViewProfile"/> is true.
    /// </summary>
    public string? ProfileIsolationKey { get; set; }

    /// <summary>
    /// Gets or sets the default invoke timeout. Null disables the default timeout.
    /// </summary>
    public TimeSpan? DefaultInvokeTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of concurrent pending JavaScript invokes.
    /// </summary>
    public int MaxPendingRequests { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether native file/folder/message dialogs are enabled.
    /// Privileged capabilities default to disabled.
    /// </summary>
    public bool EnableDialogs { get; set; }

    /// <summary>
    /// Gets or sets whether window minimize/maximize/restore/close APIs are enabled.
    /// </summary>
    public bool EnableWindowControls { get; set; }

    /// <summary>
    /// Gets or sets whether application preference storage is enabled.
    /// </summary>
    public bool EnableStorage { get; set; }

    /// <summary>
    /// Gets or sets whether desktop notifications are enabled.
    /// </summary>
    public bool EnableNotifications { get; set; }

    /// <summary>
    /// Gets or sets whether system tray integration is enabled.
    /// </summary>
    public bool EnableSystemTray { get; set; }

    /// <summary>
    /// Gets or sets an optional application company/publisher name used for storage and profile paths.
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Gets or sets an optional application name used for storage and profile paths.
    /// When null, the entry assembly name is used.
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the AppUserModelID used for notifications. When null, one is derived from the application name.
    /// </summary>
    public string? AppUserModelId { get; set; }

    /// <summary>
    /// Gets or sets an optional logger.
    /// </summary>
    public IVetraLogger? Logger { get; set; }

    /// <summary>
    /// Builds a <see cref="VetraCapabilities"/> snapshot from these options.
    /// </summary>
    public VetraCapabilities ToCapabilities() => new()
    {
        Dialogs = EnableDialogs,
        WindowControls = EnableWindowControls,
        Storage = EnableStorage,
        Notifications = EnableNotifications,
        SystemTray = EnableSystemTray
    };
}
