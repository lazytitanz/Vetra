using System.Reflection;

namespace Vetra.WinForms.Hosting;

internal static class ClientScriptProvider
{
    private static readonly Lazy<string> Script = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string GetScript() => Script.Value;

    private static string Load()
    {
        var assembly = typeof(ClientScriptProvider).Assembly;
        const string resourceName = "Vetra.WinForms.Client.vetra.js";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            var available = string.Join(", ", assembly.GetManifestResourceNames());
            throw new VetraException(
                VetraErrorCodes.InitializationFailed,
                $"Embedded Vetra client script '{resourceName}' was not found. Available: {available}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
