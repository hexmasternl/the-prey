using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class WelcomeViewModelTests
{
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<ILocationConsentGate> _consentGate = new();
    private readonly Mock<IInviteDeepLinkHandler> _deepLinkHandler = new();
    private readonly Mock<IMenuNavigator> _navigator = new();

    public WelcomeViewModelTests()
    {
        _session.Setup(s => s.TryEstablishSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SessionResult.NoGame);
        _consentGate.Setup(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _deepLinkHandler.Setup(d => d.ReplayPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
    }

    private WelcomeViewModel CreateSut() => new(
        _session.Object, _consentGate.Object, _deepLinkHandler.Object, _navigator.Object,
        NullLogger<WelcomeViewModel>.Instance);

    [Fact]
    public async Task BootstrapAsync_ShouldAwaitConsentGate_BeforeNavigatingHome()
    {
        var sut = CreateSut();

        await sut.BootstrapAsync();

        _consentGate.Verify(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>()), Times.Once);
        _navigator.Verify(n => n.GoToAsync("home"), Times.Once);
    }

    [Fact]
    public async Task BootstrapAsync_ShouldNotNavigate_WhileConsentGateIsPending()
    {
        // The gate never resolves during the assertion window, simulating a still-open disclosure /
        // the not-yet-accepted iOS consent wall — navigation must not race ahead of it.
        var gateCompletion = new TaskCompletionSource();
        _consentGate.Setup(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>())).Returns(gateCompletion.Task);
        var sut = CreateSut();

        var bootstrapTask = sut.BootstrapAsync();

        _deepLinkHandler.Verify(d => d.ReplayPendingAsync(It.IsAny<CancellationToken>()), Times.Never);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);

        gateCompletion.SetResult();
        await bootstrapTask;

        _navigator.Verify(n => n.GoToAsync("home"), Times.Once);
    }

    [Fact]
    public async Task BootstrapAsync_ShouldRunSteps_InOrder()
    {
        var order = new List<string>();
        _consentGate.Setup(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("consent"))
            .Returns(Task.CompletedTask);
        _deepLinkHandler.Setup(d => d.ReplayPendingAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("deeplink"))
            .ReturnsAsync(false);
        _navigator.Setup(n => n.GoToAsync(It.IsAny<string>()))
            .Callback(() => order.Add("navigate"))
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.BootstrapAsync();

        Assert.Equal(new[] { "consent", "deeplink", "navigate" }, order);
    }

    [Fact]
    public async Task BootstrapAsync_ShouldSkipHomeNavigation_WhenAnInviteLinkWasReplayed()
    {
        _deepLinkHandler.Setup(d => d.ReplayPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = CreateSut();

        await sut.BootstrapAsync();

        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BootstrapAsync_ShouldStillAwaitConsentGate_WhenSessionEstablishmentThrows()
    {
        _session.Setup(s => s.TryEstablishSessionAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();

        await sut.BootstrapAsync();

        _consentGate.Verify(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>()), Times.Once);
        _navigator.Verify(n => n.GoToAsync("home"), Times.Once);
    }
}
