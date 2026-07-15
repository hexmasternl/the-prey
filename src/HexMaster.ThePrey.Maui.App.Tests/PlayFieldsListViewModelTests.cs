using System.Diagnostics;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Storage;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class PlayFieldsListViewModelTests
{
    private readonly Mock<IPlayFieldApiClient> _api = new();
    private readonly Mock<IPlayFieldCache> _cache = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly FakeTimeProvider _time = new();

    private PlayFieldsListViewModel CreateSut() => new(
        _api.Object,
        _cache.Object,
        _tokens.Object,
        _time,
        NullLogger<PlayFieldsListViewModel>.Instance);

    private static IReadOnlyList<PlayFieldSummary> Fields(params string[] names) =>
        names.Select(n => new PlayFieldSummary(Guid.NewGuid(), n, false)).ToList();

    private static async Task WaitFor(Func<bool> condition, string because)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < 3000)
            await Task.Delay(10);
        Assert.True(condition(), because);
    }

    private void SetupEmptyCache() =>
        _cache.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<PlayFieldSummary>)[]);

    private void SetupToken(string? token = "token") =>
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

    // ---- Defaults ----

    [Fact]
    public void SelectedTab_DefaultsToPrivate()
    {
        var sut = CreateSut();

        Assert.Equal(PlayFieldsTab.Private, sut.SelectedTab);
        Assert.True(sut.IsPrivateSelected);
        Assert.False(sut.IsPublicSelected);
        Assert.True(sut.PublicShowPrompt);
    }

    // ---- Cache-first private load (7.4) ----

    [Fact]
    public async Task LoadPrivateAsync_ShouldPopulateFromCacheImmediately_BeforeRefreshCompletes()
    {
        _cache.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Fields("Cached-A", "Cached-B"));
        SetupToken();
        var gate = new TaskCompletionSource<MyPlayFieldsResult>();
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(gate.Task);

        var sut = CreateSut();
        var loadTask = sut.LoadPrivateAsync();

        // The cached list is visible while the refresh is still in flight.
        await WaitFor(() => sut.PrivatePlayFields.Count == 2, "the cached list shows before the refresh returns");
        Assert.Equal("Cached-A", sut.PrivatePlayFields[0].Name);
        Assert.False(sut.IsBusy); // a cached list means the refresh is non-blocking

        gate.SetResult(MyPlayFieldsResult.Success(Fields("Server-A")));
        await loadTask;

        Assert.Single(sut.PrivatePlayFields);
        Assert.Equal("Server-A", sut.PrivatePlayFields[0].Name);
    }

    [Fact]
    public async Task LoadPrivateAsync_ShouldReplaceListAndSaveCache_OnSuccessfulRefresh()
    {
        _cache.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Fields("Cached"));
        SetupToken();
        var server = Fields("Server-A", "Server-B");
        _api.Setup(a => a.GetMyPlayFieldsAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Success(server));

        var sut = CreateSut();
        await sut.LoadPrivateAsync();

        Assert.Equal(2, sut.PrivatePlayFields.Count);
        Assert.Equal("Server-A", sut.PrivatePlayFields[0].Name);
        Assert.False(sut.PrivateHasError);
        Assert.False(sut.PrivateIsEmpty);
        _cache.Verify(c => c.SaveAsync(server, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadPrivateAsync_ShouldKeepCachedList_AndShowNoError_WhenRefreshErrors()
    {
        _cache.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Fields("Cached"));
        SetupToken();
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Error);

        var sut = CreateSut();
        await sut.LoadPrivateAsync();

        Assert.Single(sut.PrivatePlayFields);
        Assert.Equal("Cached", sut.PrivatePlayFields[0].Name);
        Assert.False(sut.PrivateHasError);
        _cache.Verify(c => c.SaveAsync(It.IsAny<IReadOnlyList<PlayFieldSummary>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadPrivateAsync_ShouldKeepCachedList_WhenNoToken()
    {
        _cache.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Fields("Cached"));
        SetupToken(null);

        var sut = CreateSut();
        await sut.LoadPrivateAsync();

        Assert.Single(sut.PrivatePlayFields);
        Assert.False(sut.PrivateHasError);
        _api.Verify(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadPrivateAsync_ShouldShowError_WhenRefreshFailsWithEmptyCache()
    {
        SetupEmptyCache();
        SetupToken();
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Error);

        var sut = CreateSut();
        await sut.LoadPrivateAsync();

        Assert.Empty(sut.PrivatePlayFields);
        Assert.True(sut.PrivateHasError);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task LoadPrivateAsync_ShouldInvalidateToken_OnUnauthorized()
    {
        SetupEmptyCache();
        SetupToken();
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Unauthorized);

        var sut = CreateSut();
        await sut.LoadPrivateAsync();

        _tokens.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.PrivateHasError);
    }

    [Fact]
    public async Task LoadPrivateAsync_ShouldShowEmptyState_OnEmptySuccessWithEmptyCache()
    {
        SetupEmptyCache();
        SetupToken();
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Success([]));

        var sut = CreateSut();
        await sut.LoadPrivateAsync();

        Assert.Empty(sut.PrivatePlayFields);
        Assert.True(sut.PrivateIsEmpty);
        Assert.False(sut.PrivateHasError);
        _cache.Verify(c => c.SaveAsync(It.IsAny<IReadOnlyList<PlayFieldSummary>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Debounced public search (7.5) ----

    [Fact]
    public async Task Search_ShouldSendSingleRequestForFinalQuery_AfterDebounce()
    {
        SetupToken();
        var calls = 0;
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string q, string _, CancellationToken _) =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(PublicPlayFieldsResult.Success(Fields(q)));
            });

        var sut = CreateSut();
        sut.SearchQuery = "ri";
        sut.SearchQuery = "riv";
        sut.SearchQuery = "river";

        _time.Advance(PlayFieldsListViewModel.DebounceDelay);
        await WaitFor(() => calls >= 1, "the debounced search fires once");
        await Task.Delay(50);

        Assert.Equal(1, calls);
        _api.Verify(a => a.SearchPublicPlayFieldsAsync("river", "token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_ShouldSendNothing_AndShowPrompt_WhenQueryTooShort()
    {
        SetupToken();

        var sut = CreateSut();
        sut.SearchQuery = "ab";

        _time.Advance(PlayFieldsListViewModel.DebounceDelay);
        await WaitFor(() => sut.PublicShowPrompt, "a short query shows the prompt");

        Assert.Empty(sut.PublicPlayFields);
        _api.Verify(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_ShouldApplyOnlyLatestResults_WhenNewerQuerySupersedes()
    {
        SetupToken();
        var firstCt = new TaskCompletionSource<CancellationToken>();
        var release = new TaskCompletionSource();
        var callNo = 0;
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string q, string _, CancellationToken ct) =>
            {
                var n = Interlocked.Increment(ref callNo);
                if (n == 1)
                {
                    firstCt.TrySetResult(ct);
                    try { await release.Task.WaitAsync(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
                }
                return PublicPlayFieldsResult.Success(Fields(q));
            });

        var sut = CreateSut();
        sut.SearchQuery = "first";
        _time.Advance(PlayFieldsListViewModel.DebounceDelay);

        var ct1 = await firstCt.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(ct1.IsCancellationRequested);

        // A newer query cancels the in-flight search's token.
        sut.SearchQuery = "second";
        Assert.True(ct1.IsCancellationRequested);

        release.TrySetResult();
        _time.Advance(PlayFieldsListViewModel.DebounceDelay);
        await WaitFor(() => sut.PublicPlayFields.Count == 1 && sut.PublicPlayFields[0].Name == "second",
            "only the latest query's results are applied");
    }

    [Fact]
    public async Task Search_ShouldShowPrompt_OnValidationTooShort()
    {
        SetupToken();
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublicPlayFieldsResult.ValidationTooShort);

        var sut = CreateSut();
        sut.SearchQuery = "abc";
        _time.Advance(PlayFieldsListViewModel.DebounceDelay);

        await WaitFor(() => sut.PublicShowPrompt, "a too-short validation result shows the prompt");
        Assert.Empty(sut.PublicPlayFields);
    }

    [Fact]
    public async Task Search_ShouldShowNoResults_OnEmptySuccess()
    {
        SetupToken();
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublicPlayFieldsResult.Success([]));

        var sut = CreateSut();
        sut.SearchQuery = "zzz";
        _time.Advance(PlayFieldsListViewModel.DebounceDelay);

        await WaitFor(() => sut.PublicNoResults, "an empty result set shows no-results");
        Assert.False(sut.PublicHasError);
        Assert.False(sut.PublicShowPrompt);
    }

    [Fact]
    public async Task Search_ShouldShowError_OnError()
    {
        SetupToken();
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublicPlayFieldsResult.Error);

        var sut = CreateSut();
        sut.SearchQuery = "abc";
        _time.Advance(PlayFieldsListViewModel.DebounceDelay);

        await WaitFor(() => sut.PublicHasError, "an error result shows the error state");
    }

    [Fact]
    public async Task Search_ShouldInvalidateTokenAndShowError_OnUnauthorized()
    {
        SetupToken();
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublicPlayFieldsResult.Unauthorized);

        var sut = CreateSut();
        sut.SearchQuery = "abc";
        _time.Advance(PlayFieldsListViewModel.DebounceDelay);

        await WaitFor(() => sut.PublicHasError, "unauthorized shows the error state");
        _tokens.Verify(t => t.Invalidate(), Times.AtLeastOnce);
    }
}
