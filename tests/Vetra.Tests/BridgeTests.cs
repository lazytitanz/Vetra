using System.Text.Json;
using Vetra.Bridge;
using Vetra.Protocol;

namespace Vetra.Tests;

public sealed class BridgeTests
{
    [Fact]
    public async Task Register_And_Invoke_Returns_Result()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport, defaultInvokeTimeout: TimeSpan.FromSeconds(5));
        bridge.Register("app.getVersion", () => "1.2.3");

        await bridge.HandleIncomingAsync(InvokeJson("1", "app.getVersion"));
        var raw = await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2));
        var message = VetraJson.Deserialize<VetraMessage>(raw)!;

        Assert.Equal(VetraMessageType.Result, message.Type);
        Assert.Equal("1", message.Id);
        Assert.Equal("1.2.3", ((JsonElement)message.Data!).GetString());
    }

    [Fact]
    public async Task Duplicate_Command_Registration_Throws()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);
        bridge.Register("dup", () => true);

        var ex = Assert.Throws<VetraException>(() => bridge.Register("dup", () => false));
        Assert.Equal(VetraErrorCodes.DuplicateCommand, ex.Code);
    }

    [Fact]
    public async Task Unknown_Command_Returns_Error()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);

        await bridge.HandleIncomingAsync(InvokeJson("2", "missing.command"));
        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;

        Assert.Equal(VetraMessageType.Error, message.Type);
        Assert.Equal(VetraErrorCodes.UnknownCommand, message.Error!.Code);
    }

    [Fact]
    public async Task Typed_Arguments_Are_Deserialized()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);
        bridge.Register("app.getGreeting", (string name) => $"Hello, {name}!");

        await bridge.HandleIncomingAsync(InvokeJson("3", "app.getGreeting", "Evan"));
        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;

        Assert.Equal(VetraMessageType.Result, message.Type);
        Assert.Equal("Hello, Evan!", ((JsonElement)message.Data!).GetString());
    }

    [Fact]
    public async Task Invalid_Arguments_Return_Error()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);
        bridge.Register("app.getGreeting", (string name) => name);

        await bridge.HandleIncomingAsync(InvokeJson("4", "app.getGreeting")); // missing args
        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;

        Assert.Equal(VetraMessageType.Error, message.Type);
        Assert.Equal(VetraErrorCodes.InvalidArguments, message.Error!.Code);
    }

    [Fact]
    public async Task Async_Command_Completes()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);
        bridge.RegisterAsync("app.load", async ct =>
        {
            await Task.Delay(20, ct);
            return new { ok = true };
        });

        await bridge.HandleIncomingAsync(InvokeJson("5", "app.load"));
        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;

        Assert.Equal(VetraMessageType.Result, message.Type);
        Assert.True(((JsonElement)message.Data!).GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Handler_Exception_Returns_CommandFailed()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);
        bridge.Register("boom", () => throw new InvalidOperationException("nope"));

        await bridge.HandleIncomingAsync(InvokeJson("6", "boom"));
        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;

        Assert.Equal(VetraMessageType.Error, message.Type);
        Assert.Equal(VetraErrorCodes.CommandFailed, message.Error!.Code);
        Assert.Contains("nope", message.Error.Message);
    }

    [Fact]
    public async Task Timeout_Returns_Error()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport, defaultInvokeTimeout: TimeSpan.FromMilliseconds(50));
        bridge.RegisterAsync("slow", async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return null;
        });

        await bridge.HandleIncomingAsync(InvokeJson("7", "slow"));
        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;

        Assert.Equal(VetraMessageType.Error, message.Type);
        Assert.Equal(VetraErrorCodes.Timeout, message.Error!.Code);
    }

    [Fact]
    public async Task Cancellation_Returns_Error()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport, defaultInvokeTimeout: null);
        bridge.RegisterAsync("cancellable", async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return null;
        });

        var invoke = bridge.HandleIncomingAsync(InvokeJson("8", "cancellable"));
        await Task.Delay(30);
        bridge.CancelPending();
        await invoke;

        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;
        Assert.Equal(VetraMessageType.Error, message.Type);
        Assert.Equal(VetraErrorCodes.Cancelled, message.Error!.Code);
    }

    [Fact]
    public async Task Emit_Sends_Event_Message()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);

        await bridge.EmitAsync("app.dataUpdated", new { Count = 25 });
        var message = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;

        Assert.Equal(VetraMessageType.Event, message.Type);
        Assert.Equal("app.dataUpdated", message.Name);
        Assert.Equal(25, ((JsonElement)message.Data!).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Malformed_Json_Is_Ignored()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);
        await bridge.HandleIncomingAsync("{not-json");
        Assert.Empty(transport.Messages);
    }

    [Fact]
    public async Task Dispose_Prevents_Further_Use()
    {
        var transport = new TestTransport();
        var bridge = new VetraBridge(transport);
        await bridge.DisposeAsync();

        var ex = Assert.Throws<VetraException>(() => bridge.Register("x", () => 1));
        Assert.Equal(VetraErrorCodes.Disposed, ex.Code);

        var emit = await Assert.ThrowsAsync<VetraException>(() => bridge.EmitAsync("e"));
        Assert.Equal(VetraErrorCodes.Disposed, emit.Code);
    }

    [Fact]
    public async Task Request_Id_Correlation_Is_Preserved()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport);
        bridge.Register("echo", (string value) => value);

        await bridge.HandleIncomingAsync(InvokeJson("alpha", "echo", "a"));
        await bridge.HandleIncomingAsync(InvokeJson("beta", "echo", "b"));

        var first = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;
        var second = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;

        Assert.Contains(new[] { first.Id, second.Id }, id => id == "alpha");
        Assert.Contains(new[] { first.Id, second.Id }, id => id == "beta");
    }

    [Fact]
    public async Task Too_Many_Pending_Requests_Are_Rejected()
    {
        var transport = new TestTransport();
        await using var bridge = new VetraBridge(transport, defaultInvokeTimeout: TimeSpan.FromSeconds(5), maxPendingRequests: 1);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.RegisterAsync("hold", async ct =>
        {
            await tcs.Task.WaitAsync(ct);
            return null;
        });

        var first = bridge.HandleIncomingAsync(InvokeJson("p1", "hold"));
        await Task.Delay(30);
        await bridge.HandleIncomingAsync(InvokeJson("p2", "hold"));
        var rejected = VetraJson.Deserialize<VetraMessage>(await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2)))!;
        Assert.Equal(VetraErrorCodes.TooManyRequests, rejected.Error!.Code);

        tcs.SetResult();
        await first;
        _ = await transport.WaitForMessageAsync(TimeSpan.FromSeconds(2));
    }

    private static string InvokeJson(string id, string name, params object?[] args)
    {
        var message = new VetraMessage
        {
            Type = VetraMessageType.Invoke,
            Id = id,
            Name = name,
            Args = JsonSerializer.SerializeToElement(args, VetraJson.Options)
        };
        return VetraJson.Serialize(message);
    }
}
