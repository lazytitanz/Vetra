using Vetra.Bridge;
using Vetra.WinForms.Hosting;

namespace Vetra.WinForms.Native;

internal sealed class TrayService : IDisposable
{
    private readonly Control _host;
    private readonly IVetraBridge _bridge;
    private NotifyIcon? _notifyIcon;
    private readonly Dictionary<string, string> _menuIds = new(StringComparer.Ordinal);

    public TrayService(Control host, IVetraBridge bridge)
    {
        _host = host;
        _bridge = bridge;
    }

    public void Register(IVetraBridge bridge)
    {
        bridge.RegisterAsync<TrayConfigureOptions?>("vetra.tray.configure", async (options, _) =>
        {
            await UiThread.InvokeAsync(_host, () => Configure(options)).ConfigureAwait(false);
            return null;
        });

        bridge.RegisterAsync<bool>("vetra.tray.setVisible", async (visible, _) =>
        {
            await UiThread.InvokeAsync(_host, () =>
            {
                EnsureIcon();
                _notifyIcon!.Visible = visible;
            }).ConfigureAwait(false);
            return null;
        });

        bridge.RegisterAsync<string>("vetra.tray.setTooltip", async (text, _) =>
        {
            await UiThread.InvokeAsync(_host, () =>
            {
                EnsureIcon();
                _notifyIcon!.Text = Truncate(text);
            }).ConfigureAwait(false);
            return null;
        });

        bridge.RegisterAsync("vetra.tray.dispose", async _ =>
        {
            await UiThread.InvokeAsync(_host, DisposeIcon).ConfigureAwait(false);
            return null;
        });
    }

    private void Configure(TrayConfigureOptions? options)
    {
        EnsureIcon();
        _notifyIcon!.Text = Truncate(options?.Tooltip ?? Application.ProductName);
        _notifyIcon.Visible = options?.Visible ?? true;

        var menu = new ContextMenuStrip();
        _menuIds.Clear();

        if (options?.MenuItems is not null)
        {
            foreach (var item in options.MenuItems)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Id))
                {
                    continue;
                }

                if (string.Equals(item.Id, "-", StringComparison.Ordinal) || item.Separator)
                {
                    menu.Items.Add(new ToolStripSeparator());
                    continue;
                }

                var menuItem = new ToolStripMenuItem(item.Text ?? item.Id)
                {
                    Enabled = item.Enabled ?? true,
                    Tag = item.Id
                };
                menuItem.Click += OnMenuItemClick;
                _menuIds[item.Id] = item.Text ?? item.Id;
                menu.Items.Add(menuItem);
            }
        }

        if (options?.IncludeShowHide == true)
        {
            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
            }

            var showHide = new ToolStripMenuItem("Show / Hide") { Tag = "__vetra.showHide" };
            showHide.Click += (_, _) => ToggleMainWindow();
            menu.Items.Add(showHide);
        }

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick -= OnDoubleClick;
        _notifyIcon.DoubleClick += OnDoubleClick;
    }

    private void OnMenuItemClick(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem { Tag: string id })
        {
            _ = _bridge.EmitAsync("tray.menuClicked", new { id });
        }
    }

    private void OnDoubleClick(object? sender, EventArgs e)
    {
        ToggleMainWindow();
        _ = _bridge.EmitAsync("tray.doubleClicked", new { });
    }

    private void ToggleMainWindow()
    {
        var form = _host.FindForm();
        if (form is null)
        {
            return;
        }

        if (form.Visible)
        {
            form.Hide();
        }
        else
        {
            form.Show();
            form.WindowState = FormWindowState.Normal;
            form.Activate();
        }
    }

    private void EnsureIcon()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = false,
            Text = Truncate(Application.ProductName)
        };
    }

    private void DisposeIcon()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _menuIds.Clear();
    }

    public void Dispose() => DisposeIcon();

    private static string Truncate(string? text)
    {
        text ??= string.Empty;
        return text.Length <= 63 ? text : text[..63];
    }
}

internal sealed class TrayConfigureOptions
{
    public string? Tooltip { get; set; }
    public bool? Visible { get; set; }
    public bool IncludeShowHide { get; set; } = true;
    public List<TrayMenuItemOptions>? MenuItems { get; set; }
}

internal sealed class TrayMenuItemOptions
{
    public string? Id { get; set; }
    public string? Text { get; set; }
    public bool Separator { get; set; }
    public bool? Enabled { get; set; }
}
