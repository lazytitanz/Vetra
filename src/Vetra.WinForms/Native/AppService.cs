using System.Reflection;
using Vetra.Bridge;

namespace Vetra.WinForms.Native;

internal sealed class AppService
{
    private readonly VetraOptions _options;
    private readonly VetraCapabilities _capabilities;

    public AppService(VetraOptions options)
    {
        _options = options;
        _capabilities = options.ToCapabilities();
    }

    public void Register(IVetraBridge bridge)
    {
        bridge.Register("vetra.app.getInfo", GetInfo);
        bridge.Register("vetra.app.getCapabilities", () => _capabilities);
    }

    private object GetInfo()
    {
        var entry = Assembly.GetEntryAssembly();
        var vetraAsm = typeof(VetraHost).Assembly;

        return new
        {
            name = _options.ApplicationName
                   ?? entry?.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                   ?? entry?.GetName().Name
                   ?? Application.ProductName,
            version = entry?.GetName().Version?.ToString()
                      ?? Application.ProductVersion,
            frameworkVersion = vetraAsm.GetName().Version?.ToString() ?? "0.1.0",
            os = new
            {
                platform = "windows",
                version = Environment.OSVersion.VersionString,
                is64Bit = Environment.Is64BitOperatingSystem
            },
            runtime = new
            {
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
            }
        };
    }
}
