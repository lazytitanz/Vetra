using Vetra.Bridge;
using Vetra.WinForms.Hosting;

namespace Vetra.WinForms.Native;

internal sealed class DialogService
{
    private readonly Control _host;

    public DialogService(Control host)
    {
        _host = host;
    }

    public void Register(IVetraBridge bridge)
    {
        bridge.RegisterAsync("vetra.dialogs.openFolder", async _ =>
            await UiThread.InvokeAsync(_host, OpenFolder).ConfigureAwait(false));

        bridge.RegisterAsync<DialogFileOptions?>("vetra.dialogs.openFile", async (options, _) =>
            await UiThread.InvokeAsync(_host, () => OpenFile(options, multiSelect: false)).ConfigureAwait(false));

        bridge.RegisterAsync<DialogFileOptions?>("vetra.dialogs.openFiles", async (options, _) =>
            await UiThread.InvokeAsync(_host, () => OpenFile(options, multiSelect: true)).ConfigureAwait(false));

        bridge.RegisterAsync<DialogFileOptions?>("vetra.dialogs.saveFile", async (options, _) =>
            await UiThread.InvokeAsync(_host, () => SaveFile(options)).ConfigureAwait(false));

        bridge.RegisterAsync<DialogMessageOptions?>("vetra.dialogs.showMessage", async (options, _) =>
            await UiThread.InvokeAsync(_host, () => ShowMessage(options)).ConfigureAwait(false));
    }

    private static string? OpenFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true,
            Description = "Select a folder"
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static object? OpenFile(DialogFileOptions? options, bool multiSelect)
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = multiSelect,
            CheckFileExists = true,
            CheckPathExists = true,
            Title = options?.Title ?? (multiSelect ? "Open files" : "Open file"),
            Filter = BuildFilter(options?.Filters),
            InitialDirectory = options?.InitialDirectory ?? string.Empty
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return multiSelect ? Array.Empty<string>() : null;
        }

        return multiSelect ? dialog.FileNames : dialog.FileName;
    }

    private static string? SaveFile(DialogFileOptions? options)
    {
        using var dialog = new SaveFileDialog
        {
            OverwritePrompt = true,
            CheckPathExists = true,
            Title = options?.Title ?? "Save file",
            Filter = BuildFilter(options?.Filters),
            FileName = options?.DefaultFileName ?? string.Empty,
            InitialDirectory = options?.InitialDirectory ?? string.Empty
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    private static object ShowMessage(DialogMessageOptions? options)
    {
        var text = options?.Message ?? string.Empty;
        var caption = options?.Title ?? Application.ProductName;
        var buttons = ParseButtons(options?.Buttons);
        var icon = ParseIcon(options?.Icon);
        var result = MessageBox.Show(text, caption, buttons, icon);
        return new { result = result.ToString() };
    }

    private static string BuildFilter(IReadOnlyList<DialogFileFilter>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return "All files (*.*)|*.*";
        }

        var parts = new List<string>();
        foreach (var filter in filters)
        {
            if (filter?.Extensions is null || filter.Extensions.Count == 0)
            {
                continue;
            }

            var patterns = string.Join(";", filter.Extensions.Select(ext =>
            {
                ext = (ext ?? string.Empty).Trim().TrimStart('.');
                return $"*.{ext}";
            }));
            var name = string.IsNullOrWhiteSpace(filter.Name) ? patterns : filter.Name;
            parts.Add($"{name}|{patterns}");
        }

        return parts.Count == 0 ? "All files (*.*)|*.*" : string.Join("|", parts);
    }

    private static MessageBoxButtons ParseButtons(string? buttons) => buttons?.ToLowerInvariant() switch
    {
        "okcancel" => MessageBoxButtons.OKCancel,
        "yesno" => MessageBoxButtons.YesNo,
        "yesnocancel" => MessageBoxButtons.YesNoCancel,
        "retrycancel" => MessageBoxButtons.RetryCancel,
        "abortretryignore" => MessageBoxButtons.AbortRetryIgnore,
        _ => MessageBoxButtons.OK
    };

    private static MessageBoxIcon ParseIcon(string? icon) => icon?.ToLowerInvariant() switch
    {
        "error" => MessageBoxIcon.Error,
        "warning" => MessageBoxIcon.Warning,
        "question" => MessageBoxIcon.Question,
        "information" or "info" => MessageBoxIcon.Information,
        _ => MessageBoxIcon.None
    };
}

internal sealed class DialogFileOptions
{
    public string? Title { get; set; }
    public string? InitialDirectory { get; set; }
    public string? DefaultFileName { get; set; }
    public List<DialogFileFilter>? Filters { get; set; }
}

internal sealed class DialogFileFilter
{
    public string? Name { get; set; }
    public List<string>? Extensions { get; set; }
}

internal sealed class DialogMessageOptions
{
    public string? Title { get; set; }
    public string? Message { get; set; }
    public string? Buttons { get; set; }
    public string? Icon { get; set; }
}
