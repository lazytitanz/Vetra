using System.Collections.Concurrent;
using Microsoft.Web.WebView2.Core;

namespace Vetra.WinForms.Hosting;

/// <summary>
/// Caches <see cref="CoreWebView2Environment"/> instances by user-data folder so multiple
/// <see cref="VetraHost"/> controls in one process can share a persistent application profile.
/// </summary>
public static class WebView2EnvironmentManager
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<CoreWebView2Environment>>> Environments =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the default persistent user-data folder for an application.
    /// </summary>
    public static string GetDefaultUserDataFolder(string? companyName, string? applicationName)
    {
        var company = Sanitize(companyName) ?? "Vetra";
        var app = Sanitize(applicationName) ?? Sanitize(AppDomain.CurrentDomain.FriendlyName) ?? "App";
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            company,
            app,
            "Vetra",
            "WebView2");
    }

    /// <summary>
    /// Resolves the user-data folder for the given options.
    /// </summary>
    public static string ResolveUserDataFolder(VetraOptions options, string? hostInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        string root;
        if (!string.IsNullOrWhiteSpace(options.UserDataFolder))
        {
            root = Path.GetFullPath(options.UserDataFolder);
        }
        else
        {
            root = GetDefaultUserDataFolder(options.CompanyName, options.ApplicationName);
        }

        if (options.IsolateWebViewProfile)
        {
            var key = Sanitize(options.ProfileIsolationKey)
                      ?? Sanitize(hostInstanceId)
                      ?? Guid.NewGuid().ToString("N");
            return Path.Combine(root, key);
        }

        return root;
    }

    /// <summary>
    /// Gets or creates a shared environment for the specified user-data folder.
    /// </summary>
    public static Task<CoreWebView2Environment> GetOrCreateAsync(
        string userDataFolder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userDataFolder))
        {
            throw new ArgumentException("User data folder is required.", nameof(userDataFolder));
        }

        var fullPath = Path.GetFullPath(userDataFolder);
        Directory.CreateDirectory(fullPath);

        var lazy = Environments.GetOrAdd(
            fullPath,
            path => new Lazy<Task<CoreWebView2Environment>>(
                () => CreateEnvironmentAsync(path),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Clears the in-process environment cache. Intended for tests.
    /// </summary>
    public static void ClearCache() => Environments.Clear();

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync(string userDataFolder)
    {
        try
        {
            return await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Environments.TryRemove(userDataFolder, out _);
            throw new VetraException(
                VetraErrorCodes.WebView2Unavailable,
                "Failed to create the WebView2 environment. Ensure the Microsoft Edge WebView2 Runtime is installed.",
                innerException: ex);
        }
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value.Trim();
    }
}
