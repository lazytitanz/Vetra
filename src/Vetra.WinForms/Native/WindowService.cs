using Vetra.Bridge;
using Vetra.WinForms.Hosting;

namespace Vetra.WinForms.Native;

internal sealed class WindowService
{
    private readonly Control _host;
    private readonly IVetraBridge _bridge;
    private Form? _subscribedForm;

    public WindowService(Control host, IVetraBridge bridge)
    {
        _host = host;
        _bridge = bridge;
    }

    public void Register(IVetraBridge bridge)
    {
        bridge.RegisterAsync("vetra.window.minimize", async _ =>
        {
            await UiThread.InvokeAsync(_host, () => GetForm().WindowState = FormWindowState.Minimized).ConfigureAwait(false);
            return null;
        });
        bridge.RegisterAsync("vetra.window.maximize", async _ =>
        {
            await UiThread.InvokeAsync(_host, () => GetForm().WindowState = FormWindowState.Maximized).ConfigureAwait(false);
            return null;
        });
        bridge.RegisterAsync("vetra.window.restore", async _ =>
        {
            await UiThread.InvokeAsync(_host, () => GetForm().WindowState = FormWindowState.Normal).ConfigureAwait(false);
            return null;
        });
        bridge.RegisterAsync("vetra.window.close", async _ =>
        {
            await UiThread.InvokeAsync(_host, () => GetForm().Close()).ConfigureAwait(false);
            return null;
        });
        bridge.RegisterAsync("vetra.window.getState", async _ =>
            await UiThread.InvokeAsync(_host, () => Describe(GetForm())).ConfigureAwait(false));
        bridge.RegisterAsync("vetra.window.getTitle", async _ =>
            await UiThread.InvokeAsync(_host, () => GetForm().Text).ConfigureAwait(false));
        bridge.RegisterAsync<string>("vetra.window.setTitle", async (title, _) =>
        {
            await UiThread.InvokeAsync(_host, () => GetForm().Text = title ?? string.Empty).ConfigureAwait(false);
            return null;
        });
        bridge.RegisterAsync<bool>("vetra.window.setAlwaysOnTop", async (enabled, _) =>
        {
            await UiThread.InvokeAsync(_host, () => GetForm().TopMost = enabled).ConfigureAwait(false);
            return null;
        });
    }

    public void AttachStateEvents()
    {
        var form = GetForm();
        if (ReferenceEquals(_subscribedForm, form))
        {
            return;
        }

        DetachStateEvents();
        _subscribedForm = form;
        form.Resize += OnFormResize;
        form.VisibleChanged += OnFormVisibleChanged;
    }

    public void DetachStateEvents()
    {
        if (_subscribedForm is null)
        {
            return;
        }

        _subscribedForm.Resize -= OnFormResize;
        _subscribedForm.VisibleChanged -= OnFormVisibleChanged;
        _subscribedForm = null;
    }

    private void OnFormResize(object? sender, EventArgs e) => _ = EmitStateAsync();

    private void OnFormVisibleChanged(object? sender, EventArgs e) => _ = EmitStateAsync();

    private async Task EmitStateAsync()
    {
        try
        {
            var state = await UiThread.InvokeAsync(_host, () => Describe(GetForm())).ConfigureAwait(false);
            await _bridge.EmitAsync("window.stateChanged", state).ConfigureAwait(false);
        }
        catch
        {
            // Host may be disposing.
        }
    }

    private Form GetForm()
    {
        var form = _host.FindForm();
        if (form is null)
        {
            throw new VetraException(VetraErrorCodes.CommandFailed, "Unable to locate the owning WinForms form.");
        }

        return form;
    }

    private static object Describe(Form form) => new
    {
        state = form.WindowState.ToString().ToLowerInvariant(),
        title = form.Text,
        alwaysOnTop = form.TopMost,
        visible = form.Visible,
        bounds = new
        {
            x = form.Left,
            y = form.Top,
            width = form.Width,
            height = form.Height
        }
    };
}
