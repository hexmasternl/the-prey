using HexMaster.ThePrey.Maui.App.Configuration;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class InviteDeepLinkHandlerTests
{
    private readonly Mock<IMenuNavigator> _navigator = new();

    private InviteDeepLinkHandler CreateSut(string joinBase = "https://theprey.nl/join")
    {
        _navigator.Setup(n => n.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        var options = Options.Create(new ThePreyClientOptions { JoinLinkBaseUrl = joinBase });
        return new InviteDeepLinkHandler(_navigator.Object, options, NullLogger<InviteDeepLinkHandler>.Instance);
    }

    private static string ExpectedRoute(Guid id) =>
        $"{JoinGameViewModel.JoinRoute}?{GameLobbyViewModel.GameIdQueryKey}={id}";

    [Fact]
    public async Task TryHandleAsync_ShouldRouteOnce_WithParsedId_ForValidJoinLink()
    {
        var id = Guid.NewGuid();
        var sut = CreateSut();

        var handled = await sut.TryHandleAsync(new Uri($"https://theprey.nl/join/{id}"));

        Assert.True(handled);
        _navigator.Verify(n => n.GoToAsync(ExpectedRoute(id)), Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldIgnore_WrongHost()
    {
        var sut = CreateSut();

        var handled = await sut.TryHandleAsync(new Uri($"https://evil.example.com/join/{Guid.NewGuid()}"));

        Assert.False(handled);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldIgnore_WrongPath()
    {
        var sut = CreateSut();

        var handled = await sut.TryHandleAsync(new Uri($"https://theprey.nl/games/{Guid.NewGuid()}"));

        Assert.False(handled);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldIgnore_NonGuidId()
    {
        var sut = CreateSut();

        var handled = await sut.TryHandleAsync(new Uri("https://theprey.nl/join/not-a-guid"));

        Assert.False(handled);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldIgnore_ExtraSegments()
    {
        var sut = CreateSut();

        var handled = await sut.TryHandleAsync(new Uri($"https://theprey.nl/join/{Guid.NewGuid()}/extra"));

        Assert.False(handled);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldIgnore_NonHttpsScheme()
    {
        var sut = CreateSut();

        var handled = await sut.TryHandleAsync(new Uri($"http://theprey.nl/join/{Guid.NewGuid()}"));

        Assert.False(handled);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldIgnore_Null()
    {
        var sut = CreateSut();

        var handled = await sut.TryHandleAsync(null);

        Assert.False(handled);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReplayPendingAsync_ShouldRouteTheQueuedLink()
    {
        var id = Guid.NewGuid();
        var sut = CreateSut();
        sut.QueuePending(new Uri($"https://theprey.nl/join/{id}"));

        await sut.ReplayPendingAsync();

        _navigator.Verify(n => n.GoToAsync(ExpectedRoute(id)), Times.Once);
    }

    [Fact]
    public async Task ReplayPendingAsync_ShouldDoNothing_WhenNothingQueued()
    {
        var sut = CreateSut();

        await sut.ReplayPendingAsync();

        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReplayPendingAsync_ShouldReplayOnlyOnce()
    {
        var id = Guid.NewGuid();
        var sut = CreateSut();
        sut.QueuePending(new Uri($"https://theprey.nl/join/{id}"));

        await sut.ReplayPendingAsync();
        await sut.ReplayPendingAsync();

        _navigator.Verify(n => n.GoToAsync(ExpectedRoute(id)), Times.Once);
    }
}
