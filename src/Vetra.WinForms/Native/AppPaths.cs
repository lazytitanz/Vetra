using System.Reflection;

namespace Vetra.WinForms.Native;

internal static class AppPaths
{
    public static string GetAppDataDirectory(VetraOptions options)
    {
        var company = options.CompanyName;
        if (string.IsNullOrWhiteSpace(company))
        {
            company = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        }

        if (string.IsNullOrWhiteSpace(company))
        {
            company = "Vetra";
        }

        var app = options.ApplicationName;
        if (string.IsNullOrWhiteSpace(app))
        {
            app = Assembly.GetEntryAssembly()?.GetName().Name
                  ?? Application.ProductName
                  ?? "App";
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Sanitize(company),
            Sanitize(app));
    }

    public static string GetStorageFilePath(VetraOptions options)
        => Path.Combine(GetAppDataDirectory(options), "vetra-settings.json");

    public static string ResolveWebRoot(VetraOptions options)
    {
        var webRoot = options.WebRoot ?? "www";
        if (!Path.IsPathRooted(webRoot))
        {
            webRoot = Path.Combine(AppContext.BaseDirectory, webRoot);
        }

        return Path.GetFullPath(webRoot);
    }

    public static string Sanitize(string? value)
    {
        value ??= "App";
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? "App" : trimmed;
    }
}
