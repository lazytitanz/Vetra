using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Vetra.Bridge;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Vetra.WinForms.Native;

internal sealed class NotificationService
{
    private readonly VetraOptions _options;
    private readonly IVetraLogger _logger;
    private bool _prepared;
    private string? _appId;

    public NotificationService(VetraOptions options)
    {
        _options = options;
        _logger = options.Logger ?? NullVetraLogger.Instance;
    }

    public void Register(IVetraBridge bridge)
    {
        bridge.RegisterAsync<NotificationRequest?>("vetra.notifications.show", async (request, _) =>
        {
            await Task.Run(() => Show(request)).ConfigureAwait(false);
            return null;
        });
    }

    private void Show(NotificationRequest? request)
    {
        if (request is null || (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Message)))
        {
            throw new VetraException(
                VetraErrorCodes.InvalidArguments,
                "Notification requires a title and/or message.");
        }

        EnsurePrepared();

        try
        {
            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var nodes = xml.GetElementsByTagName("text");
            if (nodes.Count > 0)
            {
                nodes[0].AppendChild(xml.CreateTextNode(request.Title ?? string.Empty));
            }

            if (nodes.Count > 1)
            {
                nodes[1].AppendChild(xml.CreateTextNode(request.Message ?? string.Empty));
            }

            var toast = new ToastNotification(xml);
            ToastNotificationManager.CreateToastNotifier(_appId!).Show(toast);
        }
        catch (Exception ex)
        {
            throw new VetraException(
                VetraErrorCodes.NotificationFailed,
                "Failed to show a desktop notification. Ensure an AppUserModelID Start Menu shortcut exists for this application.",
                innerException: ex);
        }
    }

    private void EnsurePrepared()
    {
        if (_prepared)
        {
            return;
        }

        _appId = string.IsNullOrWhiteSpace(_options.AppUserModelId)
            ? BuildDefaultAppId()
            : _options.AppUserModelId!;

        var hr = NativeMethods.SetCurrentProcessExplicitAppUserModelID(_appId);
        if (hr < 0)
        {
            _logger.Log(VetraLogLevel.Warning, $"SetCurrentProcessExplicitAppUserModelID failed with HRESULT 0x{hr:X8}.");
        }

        try
        {
            EnsureStartMenuShortcut(_appId);
        }
        catch (Exception ex)
        {
            _logger.Log(VetraLogLevel.Warning, $"Failed to ensure Start Menu shortcut for notifications: {ex.Message}");
        }

        _prepared = true;
    }

    private string BuildDefaultAppId()
    {
        var company = string.IsNullOrWhiteSpace(_options.CompanyName) ? "Vetra" : AppPaths.Sanitize(_options.CompanyName!);
        var productName = string.IsNullOrWhiteSpace(Application.ProductName) ? "App" : Application.ProductName;
        var app = string.IsNullOrWhiteSpace(_options.ApplicationName)
            ? AppPaths.Sanitize(productName)
            : AppPaths.Sanitize(_options.ApplicationName!);
        return $"{company}.{app}";
    }

    private static void EnsureStartMenuShortcut(string appId)
    {
        var productName = string.IsNullOrWhiteSpace(Application.ProductName) ? "VetraApp" : Application.ProductName;
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var shortcutPath = Path.Combine(programs, productName + ".lnk");
        if (File.Exists(shortcutPath))
        {
            return;
        }

        var exePath = Environment.ProcessPath
                      ?? Path.Combine(AppContext.BaseDirectory, productName + ".exe");

        var shellLink = (IShellLinkW)new ShellLink();
        shellLink.SetPath(exePath);
        shellLink.SetWorkingDirectory(Path.GetDirectoryName(exePath)!);
        shellLink.SetDescription(productName);

        var propertyStore = (IPropertyStore)shellLink;
        using var appIdVariant = new PropVariant(appId);
        var key = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5); // System.AppUserModel.ID
        propertyStore.SetValue(ref key, appIdVariant);
        propertyStore.Commit();

        var persist = (IPersistFile)shellLink;
        persist.Save(shortcutPath, true);
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
    }
}

internal sealed class NotificationRequest
{
    public string? Title { get; set; }
    public string? Message { get; set; }
}

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink
{
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    void Resolve(IntPtr hwnd, uint fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
internal interface IPropertyStore
{
    void GetCount(out uint cProps);
    void GetAt(uint iProp, out PropertyKey pkey);
    void GetValue(ref PropertyKey key, [Out] PropVariant pv);
    void SetValue(ref PropertyKey key, [In] PropVariant pv);
    void Commit();
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;

    public PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }
}

[StructLayout(LayoutKind.Explicit)]
internal sealed class PropVariant : IDisposable
{
    [FieldOffset(0)] private ushort _valueType;
    [FieldOffset(8)] private IntPtr _ptr;

    public PropVariant(string value)
    {
        _valueType = (ushort)VarEnum.VT_LPWSTR;
        _ptr = Marshal.StringToCoTaskMemUni(value);
    }

    public void Dispose()
    {
        PropVariantClear(this);
        GC.SuppressFinalize(this);
    }

    ~PropVariant() => Dispose();

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear([In, Out] PropVariant pvar);
}
