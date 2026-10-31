using Vetra.Storage;

namespace Vetra.Tests;

public sealed class StorageTests
{
    [Fact]
    public async Task Set_Get_Remove_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "vetra-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var storage = new JsonFileStorage(path);

        await storage.SetAsync("theme", "dark");
        Assert.Equal("dark", await storage.GetAsync<string>("theme"));
        Assert.True(await storage.ContainsKeyAsync("theme"));
        Assert.True(await storage.RemoveAsync("theme"));
        Assert.Equal("light", await storage.GetAsync("theme", "light"));
    }

    [Fact]
    public async Task Clear_Removes_All_Keys()
    {
        var path = Path.Combine(Path.GetTempPath(), "vetra-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var storage = new JsonFileStorage(path);
        await storage.SetAsync("a", 1);
        await storage.SetAsync("b", 2);
        await storage.ClearAsync();
        Assert.False(await storage.ContainsKeyAsync("a"));
        Assert.False(await storage.ContainsKeyAsync("b"));
    }

    [Fact]
    public async Task Corrupt_File_Is_Reset()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vetra-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        await File.WriteAllTextAsync(path, "{not-json");

        var storage = new JsonFileStorage(path);
        Assert.Equal("fallback", await storage.GetAsync("missing", "fallback"));
        await storage.SetAsync("ok", true);
        Assert.True(await storage.GetAsync<bool>("ok"));
    }

    [Fact]
    public async Task Empty_Key_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), "vetra-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var storage = new JsonFileStorage(path);
        var ex = await Assert.ThrowsAsync<VetraException>(() => storage.SetAsync(" ", 1));
        Assert.Equal(VetraErrorCodes.InvalidArguments, ex.Code);
    }
}
