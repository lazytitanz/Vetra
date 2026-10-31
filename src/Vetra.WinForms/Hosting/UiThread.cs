namespace Vetra.WinForms.Hosting;

internal static class UiThread
{
    public static Task InvokeAsync(Control control, Action action)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(action);

        if (!control.IsHandleCreated || control.IsDisposed)
        {
            throw new VetraException(VetraErrorCodes.Disposed, "The UI control is not available.");
        }

        if (!control.InvokeRequired)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        control.BeginInvoke(new Action(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }));
        return tcs.Task;
    }

    public static Task<T> InvokeAsync<T>(Control control, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(func);

        if (!control.IsHandleCreated || control.IsDisposed)
        {
            throw new VetraException(VetraErrorCodes.Disposed, "The UI control is not available.");
        }

        if (!control.InvokeRequired)
        {
            return Task.FromResult(func());
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        control.BeginInvoke(new Action(() =>
        {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }));
        return tcs.Task;
    }
}
