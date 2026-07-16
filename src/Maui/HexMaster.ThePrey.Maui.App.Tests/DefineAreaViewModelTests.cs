using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class DefineAreaViewModelTests
{
    private readonly Mock<IAreaEditorNavigator> _navigator = new();

    private DefineAreaViewModel CreateSut() => new(_navigator.Object);

    private static void AddPoints(DefineAreaViewModel sut, int count)
    {
        for (var i = 0; i < count; i++)
            sut.AddVertex(52.0 + (i * 0.001), 4.0 + (i * 0.001));
    }

    // ---- Adding vertices ----

    [Fact]
    public void AddVertex_ShouldAppendVertices()
    {
        var sut = CreateSut();

        sut.AddVertex(52.1, 4.3);
        sut.AddVertex(52.2, 4.4);

        Assert.Equal(2, sut.Vertices.Count);
        Assert.Equal(new GpsCoordinate(52.1, 4.3), sut.Vertices[0]);
        Assert.Equal(new GpsCoordinate(52.2, 4.4), sut.Vertices[1]);
    }

    [Fact]
    public void AddVertex_ShouldBeNoOp_AtMaxPoints()
    {
        var sut = CreateSut();
        AddPoints(sut, DefineAreaViewModel.MaxPoints);

        sut.AddVertex(1.0, 1.0);

        Assert.Equal(DefineAreaViewModel.MaxPoints, sut.Vertices.Count);
    }

    // ---- Selection ----

    [Fact]
    public void SelectVertex_ShouldSetSingleSelection()
    {
        var sut = CreateSut();
        AddPoints(sut, 3);

        sut.SelectVertex(1);

        Assert.Equal(1, sut.SelectedIndex);
        Assert.True(sut.HasSelection);
    }

    [Fact]
    public void SelectVertex_ShouldReplacePreviousSelection()
    {
        var sut = CreateSut();
        AddPoints(sut, 3);

        sut.SelectVertex(0);
        sut.SelectVertex(2);

        Assert.Equal(2, sut.SelectedIndex);
    }

    [Fact]
    public void SelectVertex_ShouldBeIgnored_WhenOutOfRange()
    {
        var sut = CreateSut();
        AddPoints(sut, 2);

        sut.SelectVertex(5);

        Assert.Null(sut.SelectedIndex);
        Assert.False(sut.HasSelection);
    }

    // ---- Moving ----

    [Fact]
    public void MoveSelected_ShouldUpdateTheSelectedVertexCoordinate()
    {
        var sut = CreateSut();
        AddPoints(sut, 3);
        sut.SelectVertex(1);

        sut.MoveSelected(53.9, 5.9);

        Assert.Equal(new GpsCoordinate(53.9, 5.9), sut.Vertices[1]);
    }

    [Fact]
    public void MoveSelected_ShouldBeNoOp_WhenNothingSelected()
    {
        var sut = CreateSut();
        AddPoints(sut, 3);
        var before = sut.Vertices[0];

        sut.MoveSelected(53.9, 5.9);

        Assert.Equal(before, sut.Vertices[0]);
    }

    // ---- Deleting ----

    [Fact]
    public void DeleteSelected_ShouldRemoveVertexAndClearSelection()
    {
        var sut = CreateSut();
        AddPoints(sut, 3);
        sut.SelectVertex(1);
        var removed = sut.Vertices[1];

        sut.DeleteSelected();

        Assert.Equal(2, sut.Vertices.Count);
        Assert.DoesNotContain(removed, sut.Vertices);
        Assert.Null(sut.SelectedIndex);
        Assert.False(sut.HasSelection);
    }

    // ---- CanSave ----

    [Fact]
    public void CanSave_ShouldFlipToTrue_AtThreePoints()
    {
        var sut = CreateSut();

        sut.AddVertex(1, 1);
        Assert.False(sut.CanSave);
        sut.AddVertex(2, 2);
        Assert.False(sut.CanSave);
        sut.AddVertex(3, 3);
        Assert.True(sut.CanSave);

        sut.SelectVertex(0);
        sut.DeleteSelected();
        Assert.False(sut.CanSave);
    }

    // ---- Seeding ----

    [Fact]
    public void Seed_ShouldPrePopulateTheVertexCollection()
    {
        var sut = CreateSut();
        IReadOnlyList<GpsCoordinate> polygon = [new(1, 1), new(2, 2), new(3, 3)];

        sut.Seed(polygon);

        Assert.Equal(3, sut.Vertices.Count);
        Assert.True(sut.CanSave);
        Assert.Null(sut.SelectedIndex);
    }

    // ---- Save / Cancel ----

    [Fact]
    public async Task SaveCommand_ShouldReturnOrderedPoints_WhenSaveable()
    {
        var sut = CreateSut();
        AddPoints(sut, 3);
        IReadOnlyList<GpsCoordinate>? returned = null;
        _navigator.Setup(n => n.ReturnAreaAsync(It.IsAny<IReadOnlyList<GpsCoordinate>?>()))
            .Callback<IReadOnlyList<GpsCoordinate>?>(p => returned = p)
            .Returns(Task.CompletedTask);

        Assert.True(sut.SaveCommand.CanExecute(null));
        await RunAsync(sut.SaveCommand);

        Assert.NotNull(returned);
        Assert.Equal(sut.Vertices, returned);
    }

    [Fact]
    public void SaveCommand_ShouldBeDisabled_BelowThreePoints()
    {
        var sut = CreateSut();
        AddPoints(sut, 2);

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task CancelCommand_ShouldReturnNothing()
    {
        var sut = CreateSut();
        AddPoints(sut, 3);

        await RunAsync(sut.CancelCommand);

        _navigator.Verify(n => n.ReturnAreaAsync(null), Times.Once);
    }

    // ---- Centroid (edit-flow centring) ----

    [Fact]
    public void Centroid_ShouldBeNull_WhenEmpty()
    {
        var sut = CreateSut();

        Assert.Null(sut.Centroid);
    }

    [Fact]
    public void Centroid_ShouldBeTheMeanOfTheVertices()
    {
        var sut = CreateSut();
        sut.Seed([new(0, 0), new(0, 6), new(3, 0)]);

        Assert.NotNull(sut.Centroid);
        Assert.Equal(1.0, sut.Centroid!.Latitude, 9);
        Assert.Equal(2.0, sut.Centroid.Longitude, 9);
    }

    // ---- Clear ----

    [Fact]
    public void Clear_ShouldEmptyVerticesAndSelectionAndDropPolygon()
    {
        var sut = CreateSut();
        AddPoints(sut, 4);
        sut.SelectVertex(1);
        Assert.True(sut.CanSave);

        sut.Clear();

        Assert.Empty(sut.Vertices);
        Assert.Null(sut.SelectedIndex);
        Assert.False(sut.HasSelection);
        Assert.False(sut.HasVertices);
        Assert.Null(sut.Centroid);
        Assert.False(sut.CanSave);
    }

    [Fact]
    public void ClearCommand_ShouldBeDisabled_WhenNoVertices_AndEnabled_WhenSome()
    {
        var sut = CreateSut();
        Assert.False(sut.ClearCommand.CanExecute(null));

        sut.AddVertex(1, 1);

        Assert.True(sut.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void Save_ShouldBeDisabledAfterClear_UntilThreePointsAgain()
    {
        var sut = CreateSut();
        AddPoints(sut, 3);
        Assert.True(sut.SaveCommand.CanExecute(null));

        sut.Clear();
        Assert.False(sut.SaveCommand.CanExecute(null));

        AddPoints(sut, 2);
        Assert.False(sut.SaveCommand.CanExecute(null));
        sut.AddVertex(9, 9);
        Assert.True(sut.SaveCommand.CanExecute(null));
    }

    // RelayCommand.Execute is async void; the mocked tasks complete synchronously and the small delay
    // flushes any posted continuation before assertions observe the settled state.
    private static async Task RunAsync(RelayCommand command)
    {
        command.Execute(null);
        await Task.Delay(10);
    }
}
