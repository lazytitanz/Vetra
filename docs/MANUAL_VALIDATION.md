# Manual validation checklist

Automated unit tests cover the Core bridge and storage. The following require Windows + WebView2 Runtime.

## Demo

```powershell
dotnet run --project samples/Vetra.Demo -c Release
```

Verify:

- [ ] Window loads `www/index.html` from `https://app.vetra.local`
- [ ] `window.vetra` exists without a local `vetra.js` file
- [ ] Greeting / time commands succeed
- [ ] Heartbeat events appear
- [ ] Window minimize/maximize/restore work
- [ ] Folder/file dialogs return paths or cancel cleanly
- [ ] Storage set/get/remove works across relaunches
- [ ] App info and capabilities display expected flags
- [ ] Notification attempt succeeds or returns a clear error
- [ ] Tray configure shows an icon and emits menu events

## Template

```powershell
dotnet pack src/Vetra.Core -c Release -o artifacts/nuget
dotnet pack src/Vetra.WinForms -c Release -o artifacts/nuget
dotnet pack templates/Vetra.Templates -c Release -o artifacts/nuget
dotnet new install .\artifacts\nuget\Vetra.Templates.0.1.0.nupkg
dotnet new vetra -n TestApp
```

Generate **outside** this repository (root `Directory.Packages.props` would otherwise apply Central Package Management). Point `nuget.config` at `artifacts/nuget`, then `dotnet build` / `dotnet run`.

## Shared profile

Create two `VetraHost` instances in one process with default options and confirm both use the same user-data folder under LocalApplicationData.
