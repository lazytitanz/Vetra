namespace Vetra;

/// <summary>
/// Describes which native capabilities are available to JavaScript.
/// </summary>
public sealed class VetraCapabilities
{
    /// <summary>Gets or sets whether native dialogs are enabled.</summary>
    public bool Dialogs { get; set; }

    /// <summary>Gets or sets whether window controls are enabled.</summary>
    public bool WindowControls { get; set; }

    /// <summary>Gets or sets whether application storage is enabled.</summary>
    public bool Storage { get; set; }

    /// <summary>Gets or sets whether desktop notifications are enabled.</summary>
    public bool Notifications { get; set; }

    /// <summary>Gets or sets whether system tray integration is enabled.</summary>
    public bool SystemTray { get; set; }
}
