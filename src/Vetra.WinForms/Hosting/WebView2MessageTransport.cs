using Microsoft.Web.WebView2.WinForms;
using Vetra.Bridge;

namespace Vetra.WinForms.Hosting;

internal sealed class WebView2MessageTransport : IVetraMessageTransport
{
    private readonly WebView2 _webView;
    private readonly Control _marshalControl;

    public WebView2MessageTransport(WebView2 webView, Control marshalControl)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _marshalControl = marshalControl ?? throw new ArgumentNullException(nameof(marshalControl));
    }

    public Task SendAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        cancellationToken.ThrowIfCancellationRequested();

        if (_webView.CoreWebView2 is null)
        {
            throw new VetraException(VetraErrorCodes.NotInitialized, "WebView2 is not initialized.");
        }

        if (_marshalControl.InvokeRequired)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = _marshalControl.BeginInvoke(new Action(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _webView.CoreWebView2.PostWebMessageAsJson(json);
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }));
            return tcs.Task.WaitAsync(cancellationToken);
        }

        _webView.CoreWebView2.PostWebMessageAsJson(json);
        return Task.CompletedTask;
    }
}
