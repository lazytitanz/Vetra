using Vetra.WinForms;

namespace Vetra.Demo;

public sealed class MainForm : Form
{
    private readonly VetraHost _host;

    public MainForm()
    {
        Text = "Vetra Demo";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);

        _host = new VetraHost
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(_host);

        Shown += async (_, _) => await InitializeHostAsync();
    }

    private async Task InitializeHostAsync()
    {
        try
        {
            await _host.InitializeAsync(new VetraOptions
            {
                WebRoot = "www",
                StartPage = "index.html",
                EnableDevTools = true,
                EnableDialogs = true,
                EnableWindowControls = true,
                EnableStorage = true,
                EnableNotifications = true,
                EnableSystemTray = true,
                CompanyName = "Vetra",
                ApplicationName = "Vetra.Demo",
                Logger = new CallbackVetraLogger((level, message) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Vetra][{level}] {message}");
                })
            });

            _host.Bridge.Register("demo.getGreeting", (string name) =>
                $"Hello, {(string.IsNullOrWhiteSpace(name) ? "friend" : name)}!");

            _host.Bridge.RegisterAsync("demo.getTime", async ct =>
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
                return DateTimeOffset.Now.ToString("O");
            });

            _ = EmitHeartbeatAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Vetra initialization failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task EmitHeartbeatAsync()
    {
        var count = 0;
        while (!_host.IsDisposed && _host.IsInitialized)
        {
            try
            {
                count++;
                await _host.Bridge.EmitAsync("demo.heartbeat", new { count, at = DateTimeOffset.Now }).ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            try
            {
                await Task.Delay(5000).ConfigureAwait(false);
            }
            catch
            {
                break;
            }
        }
    }
}
