# Vetra

Vetra is a lightweight .NET framework for Windows desktop apps that host a local HTML/CSS/JavaScript frontend inside WinForms with Microsoft Edge WebView2.

It removes repetitive setup: WebView2 initialization, secure local asset hosting, a typed JavaScript ↔ .NET bridge, and optional native Windows capabilities.

Vetra is **not** an Electron replacement or a full UI framework. It is a hosting, communication, and native integration layer.

## What problems it solves

- Configuring WebView2 and a local `www` folder for every new app
- Hand-rolling JSON request/response correlation between JavaScript and .NET
- Reimplementing folder/file dialogs, window controls, preferences, tray icons, and notifications
- Keeping privileged native APIs off untrusted web content

## Requirements

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (Evergreen)

## Solution layout

```text
Vetra/
├── src/Vetra.Core            # Host-agnostic bridge/protocol
├── src/Vetra.WinForms       # WebView2 host + native capabilities
├── samples/Vetra.Demo       # Interactive demo
├── templates/Vetra.Templates
├── tests/Vetra.Tests
└── artifacts/nuget          # Local package output
```

## Build

```powershell
dotnet build Vetra.slnx -c Release
dotnet test tests/Vetra.Tests -c Release
```

## Install Vetra packages (local feed)

```powershell
New-Item -ItemType Directory -Force artifacts/nuget | Out-Null
dotnet pack src/Vetra.Core -c Release -o artifacts/nuget
dotnet pack src/Vetra.WinForms -c Release -o artifacts/nuget
```

`nuget.config` already maps the `VetraLocal` source to `./artifacts/nuget`.

In another project:

```powershell
dotnet add package Vetra.WinForms --version 0.1.0
```

`Vetra.WinForms` depends on `Vetra.Core` and `Microsoft.Web.WebView2`.

## Create a new application

```powershell
dotnet pack templates/Vetra.Templates -c Release -o artifacts/nuget
dotnet new install .\artifacts\nuget\Vetra.Templates.0.1.0.nupkg
dotnet new vetra -n MyNewApp
cd MyNewApp
dotnet restore
dotnet run
```

Uninstall templates:

```powershell
dotnet new uninstall Vetra.Templates
```

Generated apps do **not** ship `vetra.js`. The host injects the JavaScript client automatically.

## Integrate into an existing WinForms project

1. Target `net10.0-windows` (or `net10.0-windows10.0.17763.0`).
2. Add a package reference to `Vetra.WinForms`.
3. Copy frontend assets into `www/` and ensure they copy to the output directory.
4. Host `VetraHost` on a form:

```csharp
using Vetra;
using Vetra.WinForms;

public sealed class MainForm : Form
{
    private readonly VetraHost _host = new() { Dock = DockStyle.Fill };

    public MainForm()
    {
        Controls.Add(_host);
        Shown += async (_, _) =>
        {
            await _host.InitializeAsync(new VetraOptions
            {
                WebRoot = "www",
                StartPage = "index.html",
                EnableDialogs = true,
                EnableWindowControls = true
            });

            _host.Bridge.Register("app.getVersion", () => Application.ProductVersion);
        };
    }
}
```

## Register custom .NET commands

```csharp
host.Bridge.Register("app.getGreeting", (string name) => $"Hello, {name}!");

host.Bridge.RegisterAsync("app.loadData", async ct =>
{
    return await dataService.LoadAsync(ct);
});
```

Only explicitly registered commands are callable from JavaScript. There is no reflection-based method exposure.

## Invoke commands from JavaScript

`window.vetra` is injected before page scripts run:

```javascript
const version = await vetra.invoke("app.getVersion");
const greeting = await vetra.invoke("app.getGreeting", "Evan");
```

Errors reject the Promise with `{ code, message, details }`.

## Events

.NET:

```csharp
await host.Bridge.EmitAsync("app.dataUpdated", new { Count = 25 });
```

JavaScript:

```javascript
const unsubscribe = vetra.on("app.dataUpdated", (data) => {
  console.log(data.count);
});
```

## Native capabilities

Privileged capabilities default to **disabled**. Enable them explicitly:

```csharp
var options = new VetraOptions
{
    EnableDialogs = true,
    EnableWindowControls = true,
    EnableStorage = true,
    EnableNotifications = false,
    EnableSystemTray = false
};
```

Discover availability:

```javascript
const capabilities = await vetra.app.getCapabilities();
```

### Window

```javascript
await vetra.window.minimize();
await vetra.window.maximize();
await vetra.window.restore();
const state = await vetra.window.getState();
```

### Dialogs

```javascript
const folder = await vetra.dialogs.openFolder();
const file = await vetra.dialogs.openFile({
  filters: [{ name: "Images", extensions: ["png", "jpg"] }]
});
```

Cancelled dialogs return `null` or an empty array.

### Storage

Preferences are stored as JSON under LocalApplicationData. Do not store secrets in plaintext.

```javascript
await vetra.storage.set("theme", "dark");
const theme = await vetra.storage.get("theme", "light");
```

### Application

```javascript
const info = await vetra.app.getInfo();
```

### Notifications

Informational toasts use inbox WinRT APIs plus a Vetra-owned AppUserModelID / Start Menu shortcut helper. Interactive click activation is not implemented in v0.1.

```javascript
await vetra.notifications.show({
  title: "Task Complete",
  message: "Your operation finished."
});
```

### System tray

```javascript
await vetra.tray.configure({
  tooltip: "My App",
  visible: true,
  menuItems: [{ id: "about", text: "About" }]
});
```

## WebView2 profiles

By default, Vetra uses one persistent user-data folder per application and shares a `CoreWebView2Environment` across hosts in the same process:

`%LocalAppData%\{Company}\{App}\Vetra\WebView2`

Override with `UserDataFolder`, or set `IsolateWebViewProfile = true` for a dedicated profile.

## VB.NET

Public APIs avoid C#-only patterns. Example:

```vb
Dim host As New VetraHost With {.Dock = DockStyle.Fill}
Controls.Add(host)

Await host.InitializeAsync(New VetraOptions With {
    .WebRoot = "www",
    .StartPage = "index.html",
    .EnableDialogs = True
})

host.Bridge.Register("app.getVersion", Function() Application.ProductVersion)
```

A dedicated VB template is not shipped in v0.1; use the C# template as a structural reference.

## Publishing

```powershell
dotnet publish samples/Vetra.Demo -c Release -r win-x64 --self-contained false
dotnet publish samples/Vetra.Demo -c Release -r win-x64 --self-contained true
```

Notes:

- Frontend files in `www` remain external beside the published output (they are content files, not embedded).
- Single-file publish does not embed WebView2 native assets or `www` into one binary in a useful way; keep them adjacent and document that for users.
- Target `win-x64` for typical desktop distribution.
- ClickOnce is not required.

## Security considerations

- Local files are served from a virtual host (`https://app.vetra.local`) with `Deny` host resource access.
- Navigations and frames outside the trusted origin are cancelled.
- Bridge messages from untrusted origins are ignored.
- Only allowlisted registered commands are executable.
- Privileged native APIs are opt-in and return `capability_disabled` when turned off.
- Do not navigate the privileged host to arbitrary external websites.
- The injected client can be affected by aggressive page CSP; keep local pages free of policies that block host-injected scripts.

## Manual validation checklist

- Launch `samples/Vetra.Demo` and exercise each demo section
- Confirm `window.vetra` exists without a local `vetra.js` file
- Open a second window/host in-process and confirm the shared profile path
- `dotnet new vetra -n TestApp` builds and runs against the local feed

## License

MIT
