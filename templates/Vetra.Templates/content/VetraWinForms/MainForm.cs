using Vetra;
using Vetra.WinForms;

namespace VetraApp;

public sealed class MainForm : Form
{
    private readonly VetraHost _host = new() { Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = "VetraApp";
        Width = 960;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_host);
        Shown += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
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
                CompanyName = "VetraApp",
                ApplicationName = "VetraApp"
            });

            _host.Bridge.Register("app.getGreeting", (string name) =>
                $"Hello, {(string.IsNullOrWhiteSpace(name) ? "friend" : name)}!");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Initialization failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
