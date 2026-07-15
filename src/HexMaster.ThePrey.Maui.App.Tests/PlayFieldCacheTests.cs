using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Maui.App.Tests;

public sealed class PlayFieldCacheTests : IDisposable
{
    private readonly string _dir;

    public PlayFieldCacheTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "playfield-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private PlayFieldCache CreateSut() => new(_dir, NullLogger<PlayFieldCache>.Instance);

    private static IReadOnlyList<PlayFieldSummary> Sample() =>
    [
        new(Guid.NewGuid(), "Alpha", true),
        new(Guid.NewGuid(), "Bravo", false)
    ];

    [Fact]
    public async Task SaveThenLoad_ShouldRoundTripTheList()
    {
        var items = Sample();
        var sut = CreateSut();

        await sut.SaveAsync(items);
        var loaded = await sut.LoadAsync();

        Assert.Equal(2, loaded.Count);
        Assert.Equal(items[0].Id, loaded[0].Id);
        Assert.Equal("Alpha", loaded[0].Name);
        Assert.True(loaded[0].IsPublic);
        Assert.Equal("Bravo", loaded[1].Name);
        Assert.False(loaded[1].IsPublic);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnEmpty_WhenFileMissing()
    {
        var sut = CreateSut();

        var loaded = await sut.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnEmpty_WhenFileCorrupt()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "private-playfields.json"), "{ this is not valid json");
        var sut = CreateSut();

        var loaded = await sut.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task SaveAsync_ShouldOverwritePreviousList()
    {
        var sut = CreateSut();
        await sut.SaveAsync(Sample());

        var replacement = new PlayFieldSummary[] { new(Guid.NewGuid(), "Charlie", true) };
        await sut.SaveAsync(replacement);

        var loaded = await sut.LoadAsync();

        Assert.Single(loaded);
        Assert.Equal("Charlie", loaded[0].Name);
    }
}
