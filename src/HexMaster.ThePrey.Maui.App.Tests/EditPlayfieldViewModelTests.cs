using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class EditPlayfieldViewModelTests
{
    private readonly Mock<IPlayFieldApiClient> _api = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<IEditPlayfieldNavigator> _navigator = new();

    private const string OriginalName = "NL, Amsterdam, City park";

    private static readonly IReadOnlyList<GpsCoordinate> Triangle =
    [
        new(52.1, 4.3),
        new(52.2, 4.4),
        new(52.15, 4.5)
    ];

    private static readonly DateTimeOffset Stamp = new(2026, 7, 15, 9, 0, 0, TimeSpan.Zero);

    private EditPlayfieldViewModel CreateSut() => new(
        _api.Object, _tokens.Object, _navigator.Object,
        NullLogger<EditPlayfieldViewModel>.Instance);

    private static PlayFieldDetails Details(
        string name = OriginalName, bool isPublic = false, IReadOnlyList<GpsCoordinate>? points = null) =>
        new(Guid.NewGuid(), name, isPublic, points ?? Triangle, Stamp);

    private void SetupToken(string? token = "token") =>
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

    private void SetupGet(PlayFieldDetails details) =>
        _api.Setup(a => a.GetPlayFieldAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetPlayFieldResult.Success(details));

    private async Task<EditPlayfieldViewModel> LoadedAsync(PlayFieldDetails? details = null)
    {
        details ??= Details();
        SetupToken();
        SetupGet(details);
        var sut = CreateSut();
        await sut.LoadAsync(details.Id);
        return sut;
    }

    private void SetupUpdate(UpdatePlayFieldResult result) =>
        _api.Setup(a => a.UpdatePlayFieldAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<GpsCoordinate>>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    // ---- Load (5.3) ----

    [Fact]
    public async Task Load_ShouldPopulateFormAndSnapshot_OnSuccess()
    {
        var sut = await LoadedAsync(Details(isPublic: true));

        Assert.True(sut.IsLoaded);
        Assert.False(sut.HasLoadError);
        Assert.False(sut.IsBusy);
        Assert.Equal(OriginalName, sut.Name);
        Assert.True(sut.IsPublic);
        Assert.True(sut.HasArea);
        Assert.False(sut.CanSave); // nothing changed yet
    }

    [Fact]
    public async Task Load_ShouldShowLoadError_OnNotFound()
    {
        SetupToken();
        _api.Setup(a => a.GetPlayFieldAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetPlayFieldResult.NotFound);
        var sut = CreateSut();

        await sut.LoadAsync(Guid.NewGuid());

        Assert.True(sut.HasLoadError);
        Assert.False(sut.IsLoaded);
    }

    [Fact]
    public async Task Load_ShouldInvalidateTokenAndShowError_OnUnauthorized()
    {
        SetupToken();
        _api.Setup(a => a.GetPlayFieldAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetPlayFieldResult.Unauthorized);
        var sut = CreateSut();

        await sut.LoadAsync(Guid.NewGuid());

        _tokens.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.HasLoadError);
    }

    [Fact]
    public async Task Load_ShouldShowLoadError_WhenNoToken()
    {
        SetupToken(null);
        var sut = CreateSut();

        await sut.LoadAsync(Guid.NewGuid());

        Assert.True(sut.HasLoadError);
        _api.Verify(a => a.GetPlayFieldAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Dirty / CanSave (5.4) ----

    [Fact]
    public async Task CanSave_ShouldBeFalse_WhenNothingChanged()
    {
        var sut = await LoadedAsync();

        Assert.False(sut.IsDirty);
        Assert.False(sut.CanSave);
    }

    [Fact]
    public async Task CanSave_ShouldEnable_OnNameChange_AndDisableOnRevert()
    {
        var sut = await LoadedAsync();

        sut.Name = "NL, Rotterdam, Harbour park";
        Assert.True(sut.IsDirty);
        Assert.True(sut.CanSave);

        sut.Name = OriginalName;
        Assert.False(sut.IsDirty);
        Assert.False(sut.CanSave);
    }

    [Fact]
    public async Task CanSave_ShouldEnable_OnVisibilityChange()
    {
        var sut = await LoadedAsync(); // loads Private

        sut.IsPublic = true;

        Assert.True(sut.CanSave);
    }

    [Fact]
    public async Task CanSave_ShouldEnable_OnPolygonChange()
    {
        var sut = await LoadedAsync();
        _navigator.Setup(n => n.EditAreaAsync(It.IsAny<IReadOnlyList<GpsCoordinate>>()))
            .ReturnsAsync([new(1, 1), new(2, 2), new(3, 3)]);

        await RunAsync(sut.SetAreaCommand);

        Assert.True(sut.CanSave);
    }

    [Fact]
    public async Task CanSave_ShouldStayDisabled_WhenDirtyButNameBlank()
    {
        var sut = await LoadedAsync();

        sut.Name = "   ";

        Assert.True(sut.IsDirty);
        Assert.False(sut.CanSave);
    }

    [Fact]
    public async Task CanSave_ShouldStayDisabled_WhenDirtyButPolygonBelowThree()
    {
        var sut = await LoadedAsync();
        _navigator.Setup(n => n.EditAreaAsync(It.IsAny<IReadOnlyList<GpsCoordinate>>()))
            .ReturnsAsync([new(1, 1), new(2, 2)]);

        await RunAsync(sut.SetAreaCommand);

        Assert.False(sut.HasArea);
        Assert.False(sut.CanSave);
    }

    // ---- Toggle gating (5.5) ----

    [Fact]
    public async Task Toggle_ShouldBeEnabled_WhenLoadedNameValid()
    {
        var sut = await LoadedAsync();

        Assert.True(sut.CanTogglePublic);
    }

    [Fact]
    public async Task Toggle_ShouldDisableAndForcePrivate_WhenNameBecomesInvalid()
    {
        var sut = await LoadedAsync();
        sut.IsPublic = true;

        sut.Name = "broken name";

        Assert.False(sut.CanTogglePublic);
        Assert.False(sut.IsPublic);
    }

    // ---- Save (5.6) ----

    [Fact]
    public async Task Save_ShouldReturnUpdatedSummaryToList_OnSuccess()
    {
        var sut = await LoadedAsync();
        sut.Name = "NL, Rotterdam, Harbour park";
        var summary = new PlayFieldSummary(Guid.NewGuid(), "NL, Rotterdam, Harbour park", false);
        SetupUpdate(UpdatePlayFieldResult.Updated(summary));

        await RunAsync(sut.SaveCommand);

        _navigator.Verify(n => n.ReturnToListWithUpdateAsync(summary), Times.Once);
        Assert.False(sut.HasError);
    }

    [Fact]
    public async Task Save_ShouldShowConflictAndRetainEdits_On409()
    {
        var sut = await LoadedAsync();
        sut.Name = "NL, Rotterdam, Harbour park";
        SetupUpdate(UpdatePlayFieldResult.Conflict(Details(name: "NL, Amsterdam, Changed elsewhere")));

        await RunAsync(sut.SaveCommand);

        Assert.True(sut.HasConflict);
        Assert.Equal("NL, Rotterdam, Harbour park", sut.Name); // edits retained
        Assert.True(sut.IsLoaded); // stays open
        _navigator.Verify(n => n.ReturnToListWithUpdateAsync(It.IsAny<PlayFieldSummary?>()), Times.Never);
    }

    [Fact]
    public async Task Reload_ShouldResetToServerState_AfterConflict()
    {
        var sut = await LoadedAsync();
        sut.Name = "NL, Rotterdam, Harbour park";
        SetupUpdate(UpdatePlayFieldResult.Conflict(Details(name: "NL, Amsterdam, Server name")));
        await RunAsync(sut.SaveCommand);

        await RunAsync(sut.ReloadCommand);

        Assert.False(sut.HasConflict);
        Assert.Equal("NL, Amsterdam, Server name", sut.Name);
        Assert.False(sut.IsDirty);
    }

    [Fact]
    public async Task Save_ShouldShowValidationError_OnValidation()
    {
        var sut = await LoadedAsync();
        sut.Name = "NL, Rotterdam, Harbour park";
        SetupUpdate(UpdatePlayFieldResult.Validation);

        await RunAsync(sut.SaveCommand);

        Assert.True(sut.HasValidationError);
    }

    [Theory]
    [InlineData("forbidden")]
    [InlineData("notfound")]
    [InlineData("error")]
    public async Task Save_ShouldShowError_OnForbiddenNotFoundOrError(string kind)
    {
        var sut = await LoadedAsync();
        sut.Name = "NL, Rotterdam, Harbour park";
        SetupUpdate(kind switch
        {
            "forbidden" => UpdatePlayFieldResult.Forbidden,
            "notfound" => UpdatePlayFieldResult.NotFound,
            _ => UpdatePlayFieldResult.Error
        });

        await RunAsync(sut.SaveCommand);

        Assert.True(sut.HasError);
        _navigator.Verify(n => n.ReturnToListWithUpdateAsync(It.IsAny<PlayFieldSummary?>()), Times.Never);
    }

    [Fact]
    public async Task Save_ShouldInvalidateTokenAndError_OnUnauthorized()
    {
        var sut = await LoadedAsync();
        sut.Name = "NL, Rotterdam, Harbour park";
        SetupUpdate(UpdatePlayFieldResult.Unauthorized);

        await RunAsync(sut.SaveCommand);

        _tokens.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task Cancel_ShouldCloseWithoutUpdating()
    {
        var sut = await LoadedAsync();
        sut.Name = "NL, Rotterdam, Harbour park";

        await RunAsync(sut.CancelCommand);

        _navigator.Verify(n => n.ReturnToListWithUpdateAsync(null), Times.Once);
        _api.Verify(a => a.UpdatePlayFieldAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<GpsCoordinate>>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task RunAsync(RelayCommand command)
    {
        command.Execute(null);
        await Task.Delay(10);
    }
}
