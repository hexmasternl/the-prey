using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class CreatePlayfieldViewModelTests
{
    private readonly Mock<IPlayFieldApiClient> _api = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<ICreatePlayfieldNavigator> _navigator = new();

    private const string ValidName = "NL, Amsterdam, City park";

    private static readonly IReadOnlyList<GpsCoordinate> Triangle =
    [
        new(52.1, 4.3),
        new(52.2, 4.4),
        new(52.15, 4.5)
    ];

    private CreatePlayfieldViewModel CreateSut() => new(
        _api.Object, _tokens.Object, _navigator.Object,
        NullLogger<CreatePlayfieldViewModel>.Instance);

    private void SetupToken(string? token = "token") =>
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

    // Seeds a ≥ 3-point polygon by driving the Define-Area command through a stubbed navigator.
    private async Task DefineArea(CreatePlayfieldViewModel sut, IReadOnlyList<GpsCoordinate>? polygon = null)
    {
        _navigator.Setup(n => n.DefineAreaAsync(It.IsAny<IReadOnlyList<GpsCoordinate>>()))
            .ReturnsAsync(polygon ?? Triangle);
        await RunAsync(sut.DefineAreaCommand);
    }

    // ---- Initial state ----

    [Fact]
    public void InitialState_ShouldBeEmptyPrivateAndNotSaveable()
    {
        var sut = CreateSut();

        Assert.Equal(string.Empty, sut.Name);
        Assert.False(sut.IsPublic);
        Assert.False(sut.CanTogglePublic);
        Assert.False(sut.HasArea);
        Assert.False(sut.CanSave);
        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    // ---- Toggle gating ----

    [Fact]
    public void CanTogglePublic_ShouldEnable_WhenNameMatchesPattern()
    {
        var sut = CreateSut();

        sut.Name = ValidName;

        Assert.True(sut.CanTogglePublic);
    }

    [Fact]
    public void CanTogglePublic_ShouldStayDisabled_WhenNameInvalid()
    {
        var sut = CreateSut();

        sut.Name = "just a name";

        Assert.False(sut.CanTogglePublic);
    }

    [Fact]
    public void Name_ShouldResetToPrivate_WhenValidNameBecomesInvalid()
    {
        var sut = CreateSut();
        sut.Name = ValidName;
        sut.IsPublic = true;

        sut.Name = "broken name";

        Assert.False(sut.CanTogglePublic);
        Assert.False(sut.IsPublic);
    }

    // ---- CanSave combinations ----

    [Fact]
    public async Task CanSave_ShouldBeFalse_WithNameButNoArea()
    {
        var sut = CreateSut();
        sut.Name = ValidName;

        Assert.False(sut.CanSave);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CanSave_ShouldBeFalse_WithAreaButNoName()
    {
        var sut = CreateSut();
        await DefineArea(sut);

        Assert.True(sut.HasArea);
        Assert.False(sut.CanSave);
    }

    [Fact]
    public async Task CanSave_ShouldBeTrue_WithNameAndArea()
    {
        var sut = CreateSut();
        sut.Name = ValidName;
        await DefineArea(sut);

        Assert.True(sut.CanSave);
        Assert.True(sut.SaveCommand.CanExecute(null));
    }

    // ---- Define-Area hand-off ----

    [Fact]
    public async Task DefineAreaCommand_ShouldKeepPriorPolygon_WhenEditorCancels()
    {
        var sut = CreateSut();
        await DefineArea(sut); // establishes an area
        Assert.True(sut.HasArea);

        // Cancel returns null: the held polygon is unchanged.
        _navigator.Setup(n => n.DefineAreaAsync(It.IsAny<IReadOnlyList<GpsCoordinate>>()))
            .ReturnsAsync((IReadOnlyList<GpsCoordinate>?)null);
        await RunAsync(sut.DefineAreaCommand);

        Assert.True(sut.HasArea);
    }

    // ---- Save result mapping ----

    [Fact]
    public async Task Save_ShouldReturnCreatedSummaryToList_OnSuccess()
    {
        var sut = CreateSut();
        sut.Name = ValidName;
        sut.IsPublic = true;
        await DefineArea(sut);
        SetupToken();
        var summary = new PlayFieldSummary(Guid.NewGuid(), ValidName, true);
        _api.Setup(a => a.CreatePlayFieldAsync(ValidName, true, It.IsAny<IReadOnlyList<GpsCoordinate>>(), "token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlayFieldResult.Success(summary));

        await RunAsync(sut.SaveCommand);

        _navigator.Verify(n => n.ReturnToListAsync(summary), Times.Once);
        Assert.False(sut.HasError);
        Assert.False(sut.HasValidationError);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task Save_ShouldShowValidationError_AndKeepInputs_OnValidation()
    {
        var sut = CreateSut();
        sut.Name = ValidName;
        await DefineArea(sut);
        SetupToken();
        _api.Setup(a => a.CreatePlayFieldAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<GpsCoordinate>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlayFieldResult.Validation);

        await RunAsync(sut.SaveCommand);

        Assert.True(sut.HasValidationError);
        Assert.Equal(ValidName, sut.Name);
        Assert.True(sut.HasArea);
        _navigator.Verify(n => n.ReturnToListAsync(It.IsAny<PlayFieldSummary?>()), Times.Never);
    }

    [Fact]
    public async Task Save_ShouldInvalidateTokenAndError_OnUnauthorized()
    {
        var sut = CreateSut();
        sut.Name = ValidName;
        await DefineArea(sut);
        SetupToken();
        _api.Setup(a => a.CreatePlayFieldAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<GpsCoordinate>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlayFieldResult.Unauthorized);

        await RunAsync(sut.SaveCommand);

        _tokens.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task Save_ShouldShowError_OnTransientError()
    {
        var sut = CreateSut();
        sut.Name = ValidName;
        await DefineArea(sut);
        SetupToken();
        _api.Setup(a => a.CreatePlayFieldAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<GpsCoordinate>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlayFieldResult.Error);

        await RunAsync(sut.SaveCommand);

        Assert.True(sut.HasError);
        _navigator.Verify(n => n.ReturnToListAsync(It.IsAny<PlayFieldSummary?>()), Times.Never);
    }

    [Fact]
    public async Task Save_ShouldShowError_AndSendNoRequest_WhenNoToken()
    {
        var sut = CreateSut();
        sut.Name = ValidName;
        await DefineArea(sut);
        SetupToken(null);

        await RunAsync(sut.SaveCommand);

        Assert.True(sut.HasError);
        _api.Verify(a => a.CreatePlayFieldAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<GpsCoordinate>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Cancel ----

    [Fact]
    public async Task Cancel_ShouldCloseWithoutCreating()
    {
        var sut = CreateSut();
        sut.Name = ValidName;
        await DefineArea(sut);

        await RunAsync(sut.CancelCommand);

        _navigator.Verify(n => n.ReturnToListAsync(null), Times.Once);
        _api.Verify(a => a.CreatePlayFieldAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<GpsCoordinate>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task RunAsync(RelayCommand command)
    {
        command.Execute(null);
        await Task.Delay(10);
    }
}
