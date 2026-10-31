using Vetra.Bridge;
using Vetra.Protocol;

namespace Vetra.Tests;

public sealed class CapabilityTests
{
    [Fact]
    public async Task IgnoringArgs_Stub_Returns_CapabilityDisabled()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);
        bridge.RegisterIgnoringArgs("vetra.dialogs.openFolder", () =>
            throw new VetraException(VetraErrorCodes.CapabilityDisabled, "disabled"));

        var invoke = new VetraMessage
        {
            Type = VetraMessageType.Invoke,
            Id = "c1",
            Name = "vetra.dialogs.openFolder",
            Args = System.Text.Json.JsonSerializer.SerializeToElement(new object[] { new { x = 1 } }, VetraJson.Options)
        };

        await bridge.HandleIncomingAsync(VetraJson.Serialize(invoke));
        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;
        Assert.Equal(VetraErrorCodes.CapabilityDisabled, message.Error!.Code);
    }

    [Fact]
    public void Options_ToCapabilities_Reflects_Flags()
    {
        var options = new VetraOptions
        {
            EnableDialogs = true,
            EnableStorage = true,
            EnableNotifications = false
        };

        var caps = options.ToCapabilities();
        Assert.True(caps.Dialogs);
        Assert.True(caps.Storage);
        Assert.False(caps.Notifications);
        Assert.False(caps.WindowControls);
        Assert.False(caps.SystemTray);
    }
}
