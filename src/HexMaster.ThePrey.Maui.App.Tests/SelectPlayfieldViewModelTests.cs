using System.Diagnostics;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Storage;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class SelectPlayfieldViewModelTests
{
    private readonly Mock<IPlayFieldApiClient> _api = new();
    private readonly Mock<IPlayFieldCache> _cache = new();
    private readonly Mock<IAccessTokenProvider> _tokenProvider = new();
    private readonly Mock<IPlayfieldSelectResultSink> _sink = new();
    private readonly FakeTimeProvider _time = new();

    public SelectPlayfieldViewModelTests()
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _cache.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<PlayFieldSummary>)[]);
        _sink.Setup(s => s.CompleteAsync(It.IsAny<PlayFieldSummary?>())).Returns(Task.CompletedTask);
    }

    private SelectPlayfieldViewModel CreateSut() => new(
        _api.Object, _cache.Object, _tokenProvider.Object, _sink.Object, _time,
        NullLogger<SelectPlayfieldViewModel>.Instance);

    private static PlayFieldSummary Pf(string name, bool isPublic = false, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), name, isPublic);

    private void SetupCache(params PlayFieldSummary[] items) =>
        _cache.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(items);

    private void SetupMine(params PlayFieldSummary[] items) =>
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Success(items));

    private void SetupPublic(params PlayFieldSummary[] items) =>
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublicPlayFieldsResult.Success(items));

    private static async Task WaitFor(Func<bool> condition, string because)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < 3000)
            await Task.Delay(10);
        Assert.True(condition(), because);
    }

    // ---- Default load ----

    [Fact]
    public async Task LoadDefault_ShouldShowCachedImmediately_AndReplaceAndSaveOnRefresh()
    {
        SetupCache(Pf("Cached Field"));
        var fresh = new[] { Pf("Harbour"), Pf("Docks") };
        SetupMine(fresh);
        var sut = CreateSut();

        await sut.LoadDefaultAsync();

        Assert.Equal(2, sut.Items.Count);
        Assert.Contains(sut.Items, i => i.Name == "Harbour");
        _cache.Verify(c => c.SaveAsync(fresh, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.HasError);
    }

    [Fact]
    public async Task LoadDefault_ShouldKeepCachedList_AndNotError_WhenRefreshFails()
    {
        SetupCache(Pf("Cached Field"));
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Error);
        var sut = CreateSut();

        await sut.LoadDefaultAsync();

        Assert.Single(sut.Items);
        Assert.False(sut.HasError);
    }

    [Fact]
    public async Task LoadDefault_ShouldError_WhenRefreshFailsWithEmptyCache()
    {
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Error);
        var sut = CreateSut();

        await sut.LoadDefaultAsync();

        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task LoadDefault_ShouldBeEmpty_WhenNoCacheAndRefreshReturnsNone()
    {
        SetupMine();
        var sut = CreateSut();

        await sut.LoadDefaultAsync();

        Assert.Empty(sut.Items);
        Assert.True(sut.IsEmpty);
        Assert.False(sut.HasError);
    }

    [Fact]
    public async Task LoadDefault_ShouldInvalidateToken_WhenUnauthorized()
    {
        _api.Setup(a => a.GetMyPlayFieldsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MyPlayFieldsResult.Unauthorized);
        var sut = CreateSut();

        await sut.LoadDefaultAsync();

        _tokenProvider.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.HasError);
    }

    // ---- Debounced merged search ----

    [Fact]
    public async Task Search_ShouldSendSingleRequestForFinalQuery_AfterDebounce()
    {
        SetupMine();
        var calls = 0;
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { Interlocked.Increment(ref calls); return PublicPlayFieldsResult.Success([]); });
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        sut.SearchQuery = "ha";
        sut.SearchQuery = "har";
        sut.SearchQuery = "harb";
        _time.Advance(SelectPlayfieldViewModel.DebounceDelay);
        await WaitFor(() => calls >= 1, "the debounced search fires once");
        await Task.Delay(50);

        Assert.Equal(1, calls);
        _api.Verify(a => a.SearchPublicPlayFieldsAsync("harb", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_ShouldNotSendRequest_AndRestoreDefault_WhenBelowThreeChars()
    {
        SetupMine(Pf("Harbour"));
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        sut.SearchQuery = "ha";
        _time.Advance(SelectPlayfieldViewModel.DebounceDelay);
        await WaitFor(() => sut.Items.Count == 1, "a short query restores the own list");

        _api.Verify(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("Harbour", sut.Items[0].Name);
    }

    [Fact]
    public async Task Search_ShouldMergeOwnAndPublic_DeDupedByIdWithOwnWinning()
    {
        var shared = Guid.NewGuid();
        // Own list: a private field and a public one that will also come back from the public search.
        SetupMine(Pf("Harbour Private"), Pf("Harbour Shared", isPublic: true, id: shared));
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        // Public search returns the shared one (dup) plus one from another owner.
        SetupPublic(Pf("Harbour Shared", isPublic: true, id: shared), Pf("Harbour External", isPublic: true));

        sut.SearchQuery = "harbour";
        _time.Advance(SelectPlayfieldViewModel.DebounceDelay);
        await WaitFor(() => sut.Items.Count == 3, "own private + own public + external public merge, shared deduped");

        Assert.Single(sut.Items, i => i.Id == shared);
        Assert.Contains(sut.Items, i => i.Name == "Harbour Private");
        Assert.Contains(sut.Items, i => i.Name == "Harbour External");
    }

    [Fact]
    public async Task Search_ShouldShowNoResults_WhenMergeEmpty()
    {
        SetupMine(Pf("Docks"));
        SetupPublic();
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        sut.SearchQuery = "zzz";
        _time.Advance(SelectPlayfieldViewModel.DebounceDelay);
        await WaitFor(() => sut.ShowNoResults, "an empty merge shows the no-results state");

        Assert.Empty(sut.Items);
    }

    [Fact]
    public async Task Search_ShouldRestoreDefault_OnValidationTooShort()
    {
        SetupMine(Pf("Harbour"));
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublicPlayFieldsResult.ValidationTooShort);
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        sut.SearchQuery = "har";
        _time.Advance(SelectPlayfieldViewModel.DebounceDelay);
        await WaitFor(() => sut.Items.Count == 1 && !sut.HasError, "ValidationTooShort restores the own list");

        Assert.Equal("Harbour", sut.Items[0].Name);
    }

    [Fact]
    public async Task Search_ShouldShowError_OnPublicSearchError()
    {
        SetupMine(Pf("Harbour"));
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublicPlayFieldsResult.Error);
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        sut.SearchQuery = "har";
        _time.Advance(SelectPlayfieldViewModel.DebounceDelay);
        await WaitFor(() => sut.HasError, "a public search error shows the error state");
    }

    [Fact]
    public async Task Search_Unauthorized_ShouldInvalidateTokenAndError()
    {
        SetupMine(Pf("Harbour"));
        _api.Setup(a => a.SearchPublicPlayFieldsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublicPlayFieldsResult.Unauthorized);
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        sut.SearchQuery = "har";
        _time.Advance(SelectPlayfieldViewModel.DebounceDelay);
        await WaitFor(() => sut.HasError, "an unauthorized search shows the error state");

        _tokenProvider.Verify(t => t.Invalidate(), Times.Once);
    }

    // ---- Selection & confirm ----

    [Fact]
    public async Task Selection_ShouldToggleAndDriveCanSelect()
    {
        SetupMine(Pf("A"), Pf("B"));
        var sut = CreateSut();
        await sut.LoadDefaultAsync();
        Assert.False(sut.CanSelect);

        var a = sut.Items[0];
        var b = sut.Items[1];

        sut.ToggleSelect(a);
        Assert.True(a.IsSelected);
        Assert.True(sut.CanSelect);

        // Selecting another row moves the selection.
        sut.ToggleSelect(b);
        Assert.False(a.IsSelected);
        Assert.True(b.IsSelected);
        Assert.Same(b, sut.SelectedItem);

        // Re-tapping the selected row clears it.
        sut.ToggleSelect(b);
        Assert.False(b.IsSelected);
        Assert.Null(sut.SelectedItem);
        Assert.False(sut.CanSelect);
    }

    [Fact]
    public async Task Confirm_ShouldHandSelectedSummaryToSink()
    {
        var pf = Pf("Harbour");
        SetupMine(pf);
        var sut = CreateSut();
        await sut.LoadDefaultAsync();
        sut.ToggleSelect(sut.Items[0]);

        await sut.ConfirmAsync();

        _sink.Verify(s => s.CompleteAsync(pf), Times.Once);
    }

    [Fact]
    public async Task Confirm_ShouldDoNothing_WhenNothingSelected()
    {
        SetupMine(Pf("Harbour"));
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        await sut.ConfirmAsync();

        _sink.Verify(s => s.CompleteAsync(It.IsAny<PlayFieldSummary?>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_ShouldHandNullToSink()
    {
        SetupMine(Pf("Harbour"));
        var sut = CreateSut();
        await sut.LoadDefaultAsync();

        await sut.CancelAsync();

        _sink.Verify(s => s.CompleteAsync(null), Times.Once);
    }
}
