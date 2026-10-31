using System.ComponentModel;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Vetra.Bridge;
using Vetra.Storage;
using Vetra.WinForms.Hosting;
using Vetra.WinForms.Native;

namespace Vetra.WinForms;

/// <summary>
/// WinForms control that hosts WebView2, serves a local frontend, and exposes the Vetra bridge.
/// </summary>
[ToolboxItem(true)]
[Description("Hosts a local HTML/CSS/JS frontend with a secure Vetra .NET bridge.")]
public class VetraHost : UserControl
{
    private readonly WebView2 _webView;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    private VetraOptions? _options;
    private VetraBridge? _bridge;
    private WindowService? _windowService;
    private TrayService? _trayService;
    private string? _scriptId;
    private string? _trustedOrigin;
    private string? _webRoot;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VetraHost"/> class.
    /// </summary>
    public VetraHost()
    {
        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(_webView);
    }

    /// <summary>
    /// Gets the command bridge. Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IVetraBridge Bridge =>
        _bridge ?? throw new VetraException(
            VetraErrorCodes.NotInitialized,
            "VetraHost has not been initialized. Call InitializeAsync first.");

    /// <summary>
    /// Gets a value indicating whether the host has been initialized.
    /// </summary>
    [Browsable(false)]
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Gets the options used during initialization.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public VetraOptions? Options => _options;

    /// <summary>
    /// Gets the underlying WebView2 control.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WebView2 WebView => _webView;

    /// <summary>
    /// Initializes WebView2, maps local assets, injects the Vetra JavaScript client, and registers native capabilities.
    /// </summary>
    public async Task InitializeAsync(VetraOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        options ??= new VetraOptions();

        await _initGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            ThrowIfDisposed();
            if (_initialized)
            {
                throw new VetraException(
                    VetraErrorCodes.InitializationFailed,
                    "VetraHost has already been initialized.");
            }

            _options = options;
            var logger = options.Logger ?? NullVetraLogger.Instance;

            _webRoot = AppPaths.ResolveWebRoot(options);
            if (!Directory.Exists(_webRoot))
            {
                throw new VetraException(
                    VetraErrorCodes.InitializationFailed,
                    $"Web root directory was not found: '{_webRoot}'.");
            }

            var startPage = string.IsNullOrWhiteSpace(options.StartPage) ? "index.html" : options.StartPage.TrimStart('/', '\\');
            var startPagePath = Path.GetFullPath(Path.Combine(_webRoot, startPage));
            if (!IsPathUnderRoot(_webRoot, startPagePath))
            {
                throw new VetraException(
                    VetraErrorCodes.InitializationFailed,
                    "Start page resolves outside of the configured web root.");
            }

            if (!File.Exists(startPagePath))
            {
                throw new VetraException(
                    VetraErrorCodes.InitializationFailed,
                    $"Start page was not found: '{startPagePath}'.");
            }

            var userDataFolder = WebView2EnvironmentManager.ResolveUserDataFolder(options, _instanceId);
            logger.Log(VetraLogLevel.Information, $"Using WebView2 user data folder '{userDataFolder}'.");

            CoreWebView2Environment environment;
            try
            {
                environment = await WebView2EnvironmentManager.GetOrCreateAsync(userDataFolder, cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (VetraException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new VetraException(
                    VetraErrorCodes.WebView2Unavailable,
                    "Microsoft Edge WebView2 Runtime was not found or is incompatible. Install the Evergreen Runtime and try again.",
                    innerException: ex);
            }

            try
            {
                await _webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                throw new VetraException(
                    VetraErrorCodes.WebView2Unavailable,
                    "Failed to initialize WebView2. Install or repair the Microsoft Edge WebView2 Runtime.",
                    innerException: ex);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var core = _webView.CoreWebView2
                        ?? throw new VetraException(VetraErrorCodes.InitializationFailed, "CoreWebView2 was not created.");

            core.Settings.AreDefaultContextMenusEnabled = options.EnableDevTools;
            core.Settings.AreDevToolsEnabled = options.EnableDevTools;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = true;

            var hostName = string.IsNullOrWhiteSpace(options.VirtualHostName)
                ? "app.vetra.local"
                : options.VirtualHostName.Trim();
            _trustedOrigin = $"https://{hostName}";

            core.SetVirtualHostNameToFolderMapping(
                hostName,
                _webRoot,
                CoreWebView2HostResourceAccessKind.Deny);

            var transport = new WebView2MessageTransport(_webView, this);
            _bridge = new VetraBridge(
                transport,
                options.DefaultInvokeTimeout,
                options.MaxPendingRequests,
                logger);

            RegisterBuiltInCommands(options, _bridge);

            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationStarting += OnNavigationStarting;
            core.FrameNavigationStarting += OnFrameNavigationStarting;

            var script = ClientScriptProvider.GetScript();
            _scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script).ConfigureAwait(true);

            var startUri = new Uri($"{_trustedOrigin}/{startPage.Replace('\\', '/')}");
            _webView.Source = startUri;

            _windowService?.AttachStateEvents();
            _initialized = true;
            logger.Log(VetraLogLevel.Information, $"VetraHost initialized at {startUri}.");
        }
        finally
        {
            _initGate.Release();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            try
            {
                _bridge?.CancelPending();
                _windowService?.DetachStateEvents();
                _trayService?.Dispose();

                if (_webView.CoreWebView2 is not null)
                {
                    _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                    _webView.CoreWebView2.NavigationStarting -= OnNavigationStarting;
                    _webView.CoreWebView2.FrameNavigationStarting -= OnFrameNavigationStarting;

                    if (!string.IsNullOrEmpty(_scriptId))
                    {
                        _webView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(_scriptId);
                    }
                }

                _bridge?.Dispose();
                _webView.Dispose();
                _initGate.Dispose();
            }
            catch
            {
                // Best-effort cleanup during dispose.
            }
        }

        base.Dispose(disposing);
    }

    private void RegisterBuiltInCommands(VetraOptions options, IVetraBridge bridge)
    {
        new AppService(options).Register(bridge);

        if (options.EnableWindowControls)
        {
            _windowService = new WindowService(this, bridge);
            _windowService.Register(bridge);
        }
        else
        {
            RegisterDisabled(bridge, "vetra.window.", "windowControls");
        }

        if (options.EnableDialogs)
        {
            new DialogService(this).Register(bridge);
        }
        else
        {
            RegisterDisabled(bridge, "vetra.dialogs.", "dialogs");
        }

        if (options.EnableStorage)
        {
            var storage = new JsonFileStorage(AppPaths.GetStorageFilePath(options), options.Logger);
            new StorageService(storage).Register(bridge);
        }
        else
        {
            RegisterDisabled(bridge, "vetra.storage.", "storage");
        }

        if (options.EnableNotifications)
        {
            new NotificationService(options).Register(bridge);
        }
        else
        {
            RegisterDisabled(bridge, "vetra.notifications.", "notifications");
        }

        if (options.EnableSystemTray)
        {
            _trayService = new TrayService(this, bridge);
            _trayService.Register(bridge);
        }
        else
        {
            RegisterDisabled(bridge, "vetra.tray.", "systemTray");
        }
    }

    private static void RegisterDisabled(IVetraBridge bridge, string prefix, string capability)
    {
        // Named stubs so invoke returns CapabilityDisabled instead of UnknownCommand.
        string[] names = prefix switch
        {
            "vetra.window." =>
            [
                "vetra.window.minimize", "vetra.window.maximize", "vetra.window.restore", "vetra.window.close",
                "vetra.window.getState", "vetra.window.getTitle", "vetra.window.setTitle", "vetra.window.setAlwaysOnTop"
            ],
            "vetra.dialogs." =>
            [
                "vetra.dialogs.openFolder", "vetra.dialogs.openFile", "vetra.dialogs.openFiles",
                "vetra.dialogs.saveFile", "vetra.dialogs.showMessage"
            ],
            "vetra.storage." =>
            [
                "vetra.storage.get", "vetra.storage.set", "vetra.storage.remove", "vetra.storage.clear"
            ],
            "vetra.notifications." => ["vetra.notifications.show"],
            "vetra.tray." =>
            [
                "vetra.tray.configure", "vetra.tray.setVisible", "vetra.tray.setTooltip", "vetra.tray.dispose"
            ],
            _ => Array.Empty<string>()
        };

        foreach (var name in names)
        {
            bridge.RegisterIgnoringArgs(name, () =>
            {
                CapabilityGuard.Ensure(false, capability);
                return null;
            });
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_bridge is null || _disposed)
        {
            return;
        }

        try
        {
            var source = e.Source;
            if (!IsTrustedOrigin(source))
            {
                _options?.Logger?.Log(
                    VetraLogLevel.Warning,
                    $"Ignored web message from untrusted origin '{source}'.");
                return;
            }

            var json = e.WebMessageAsJson;
            _ = _bridge.HandleIncomingAsync(json);
        }
        catch (Exception ex)
        {
            _options?.Logger?.Log(VetraLogLevel.Error, $"Failed to handle web message: {ex.Message}");
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!IsTrustedOrigin(e.Uri))
        {
            e.Cancel = true;
            _options?.Logger?.Log(VetraLogLevel.Warning, $"Blocked navigation to untrusted URI '{e.Uri}'.");
        }
    }

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!IsTrustedOrigin(e.Uri))
        {
            e.Cancel = true;
            _options?.Logger?.Log(VetraLogLevel.Warning, $"Blocked frame navigation to untrusted URI '{e.Uri}'.");
        }
    }

    private bool IsTrustedOrigin(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(_trustedOrigin))
        {
            return false;
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        return string.Equals(parsed.GetLeftPart(UriPartial.Authority), _trustedOrigin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathUnderRoot(string root, string path)
    {
        var rootFull = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var pathFull = Path.GetFullPath(path);
        return pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   pathFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new VetraException(VetraErrorCodes.Disposed, "VetraHost has been disposed.");
        }
    }
}
