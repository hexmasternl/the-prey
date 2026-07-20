using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Location;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameLocationTrackerCoordinatorTests
{
    private readonly Mock<IContinuousLocationSource> _source = new() { DefaultValue = DefaultValue.Empty };
    private readonly Mock<IBackgroundExecutionHost> _host = new() { DefaultValue = DefaultValue.Empty };
    private readonly Mock<ILocationReportClient> _report = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<ILocationConsentGate> _consent = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly LocationSample _sample =
        new(52.1, 4.3, 5.0, new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

    public GameLocationTrackerCoordinatorTests()
    {
        _source.Setup(s => s.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_sample);
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _report.Setup(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationReportResult.Accepted(new RecordLocationResponse(true, 10)));
        // Consent granted by default so the existing lifecycle/cadence/resilience tests below are
        // unaffected by the consent gate; the decline path is covered explicitly further down.
        _consent.Setup(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private GameLocationTrackerCoordinator CreateSut() => new(
        _source.Object, _host.Object, _report.Object, _tokens.Object, _consent.Object, _time,
        NullLogger<GameLocationTrackerCoordinator>.Instance);

    // --- 8.1 Coordinator reports a fix each tick with a bearer token ---

    [Fact]
    public async Task StartAsync_ShouldReportFixWithBearerToken()
    {
        var sut = CreateSut();

        await sut.StartAsync(_gameId);

        _report.Verify(r => r.ReportAsync(
            _gameId,
            It.Is<RecordLocationRequest>(req => req.Latitude == 52.1 && req.Longitude == 4.3 && req.Accuracy == 5.0),
            "token",
            It.IsAny<CancellationToken>()), Times.Once);

        await sut.StopAsync();
    }

    [Fact]
    public async Task ExecuteTickAsync_ShouldSkipWithoutReporting_WhenNoFix()
    {
        _source.Setup(s => s.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync((LocationSample?)null);
        var sut = CreateSut();

        var kind = await sut.ExecuteTickAsync(_gameId, CancellationToken.None);

        Assert.Equal(GameLocationTrackerCoordinator.TickKind.Skipped, kind);
        _report.Verify(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- 8.2 Cadence: 10 s default, adopts NextLocationIntervalSeconds, clamps non-positive ---

    [Fact]
    public void CurrentInterval_ShouldDefaultToTenSeconds_BeforeAnyResponse()
    {
        var sut = CreateSut();

        Assert.Equal(TimeSpan.FromSeconds(10), sut.CurrentInterval);
    }

    [Fact]
    public async Task ExecuteTickAsync_ShouldAdoptServerInterval()
    {
        _report.Setup(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationReportResult.Accepted(new RecordLocationResponse(true, 30)));
        var sut = CreateSut();

        await sut.ExecuteTickAsync(_gameId, CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(30), sut.CurrentInterval);
    }

    [Fact]
    public void AdoptInterval_ShouldClampNonPositive_ToMinimum()
    {
        Assert.Equal(GameLocationTrackerCoordinator.MinInterval, GameLocationTrackerCoordinator.AdoptInterval(new RecordLocationResponse(true, 0)));
        Assert.Equal(GameLocationTrackerCoordinator.MinInterval, GameLocationTrackerCoordinator.AdoptInterval(new RecordLocationResponse(true, -5)));
    }

    [Fact]
    public void AdoptInterval_ShouldClampTinyInterval_ToMinimum()
    {
        Assert.Equal(GameLocationTrackerCoordinator.MinInterval, GameLocationTrackerCoordinator.AdoptInterval(new RecordLocationResponse(true, 1)));
    }

    [Fact]
    public void AdoptInterval_ShouldPreferActivePenaltyInterval()
    {
        var result = GameLocationTrackerCoordinator.AdoptInterval(new RecordLocationResponse(true, 30, PenaltyIntervalSeconds: 8));

        Assert.Equal(TimeSpan.FromSeconds(8), result);
    }

    // --- 8.3 Lifecycle: idempotent start, no-op stop, stops on game-over and on explicit stop ---

    [Fact]
    public async Task StartAsync_ShouldBeIdempotent_ForSameGame()
    {
        var sut = CreateSut();

        await sut.StartAsync(_gameId);
        await sut.StartAsync(_gameId);

        _host.Verify(h => h.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(sut.IsTracking);

        await sut.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldBeNoOp_WhenNotTracking()
    {
        var sut = CreateSut();

        await sut.StopAsync();

        _host.Verify(h => h.StopAsync(), Times.Never);
        Assert.False(sut.IsTracking);
    }

    [Fact]
    public async Task StopAsync_ShouldStopHostAndSource_WhenTracking()
    {
        var sut = CreateSut();
        await sut.StartAsync(_gameId);

        await sut.StopAsync();

        _host.Verify(h => h.StopAsync(), Times.Once);
        _source.Verify(s => s.StopAsync(), Times.Once);
        Assert.False(sut.IsTracking);
    }

    [Fact]
    public async Task StartAsync_ShouldNotTrack_WhenFirstReportSaysGameOver()
    {
        _report.Setup(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationReportResult.GameOver);
        var sut = CreateSut();

        await sut.StartAsync(_gameId);

        Assert.False(sut.IsTracking);
        _host.Verify(h => h.StopAsync(), Times.Once);
    }

    // --- Background-location consent gate: awaited before the source/host are ever touched ---

    [Fact]
    public async Task StartAsync_ShouldStartSourceAndHost_WhenConsentGranted()
    {
        var sut = CreateSut();

        await sut.StartAsync(_gameId);

        _consent.Verify(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>()), Times.Once);
        _source.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        _host.Verify(h => h.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(sut.IsTracking);

        await sut.StopAsync();
    }

    [Fact]
    public async Task StartAsync_ShouldNotStartSourceOrHost_WhenConsentDeclined()
    {
        _consent.Setup(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var sut = CreateSut();

        await sut.StartAsync(_gameId);

        _source.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        _host.Verify(h => h.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        _report.Verify(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(sut.IsTracking);
    }

    [Fact]
    public async Task StartAsync_ShouldReAskConsent_OnNextAttempt_AfterADecline()
    {
        _consent.SetupSequence(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        var sut = CreateSut();

        await sut.StartAsync(_gameId); // Declined — nothing starts.
        Assert.False(sut.IsTracking);

        await sut.StartAsync(_gameId); // A later attempt re-shows the disclosure and now proceeds.

        _consent.Verify(c => c.EnsureConsentAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.True(sut.IsTracking);

        await sut.StopAsync();
    }

    // --- 8.4 Resilience: transient failures keep tracking; token refresh attempted on 401 ---

    [Fact]
    public async Task ExecuteTickAsync_ShouldStayTransient_WhenReportIsTransient()
    {
        _report.Setup(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationReportResult.Transient);
        var sut = CreateSut();

        var kind = await sut.ExecuteTickAsync(_gameId, CancellationToken.None);

        Assert.Equal(GameLocationTrackerCoordinator.TickKind.Transient, kind);
    }

    [Fact]
    public async Task ExecuteTickAsync_ShouldInvalidateToken_When401()
    {
        _report.Setup(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationReportResult.Unauthorized);
        var sut = CreateSut();

        var kind = await sut.ExecuteTickAsync(_gameId, CancellationToken.None);

        Assert.Equal(GameLocationTrackerCoordinator.TickKind.Transient, kind);
        _tokens.Verify(t => t.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task ExecuteTickAsync_ShouldSignalGameOver_When404Or422()
    {
        _report.Setup(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LocationReportResult.GameOver);
        var sut = CreateSut();

        var kind = await sut.ExecuteTickAsync(_gameId, CancellationToken.None);

        Assert.Equal(GameLocationTrackerCoordinator.TickKind.GameOver, kind);
    }

    [Fact]
    public async Task ExecuteTickAsync_ShouldReturnTransient_WhenTokenUnavailable()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var sut = CreateSut();

        var kind = await sut.ExecuteTickAsync(_gameId, CancellationToken.None);

        Assert.Equal(GameLocationTrackerCoordinator.TickKind.Transient, kind);
        _report.Verify(r => r.ReportAsync(It.IsAny<Guid>(), It.IsAny<RecordLocationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- 8.5 Failsafe deadline: tracking stops once the game's known duration (plus grace) elapses ---

    [Fact]
    public async Task StartAsync_ShouldStopTracking_WhenRemainingDurationElapses()
    {
        var sut = CreateSut();

        // Deadline = now + 5s remaining + 30s grace = 35s; each cadence step (10s) advances toward it.
        await sut.StartAsync(_gameId, TimeSpan.FromSeconds(5));
        Assert.True(sut.IsTracking);

        await AdvanceUntilAsync(() => !sut.IsTracking, TimeSpan.FromSeconds(10));

        Assert.False(sut.IsTracking);
        _host.Verify(h => h.StopAsync(), Times.Once);
        _source.Verify(s => s.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_ShouldKeepTracking_WhenNoRemainingGiven()
    {
        var sut = CreateSut();

        await sut.StartAsync(_gameId); // No deadline — only a server game-over or explicit stop ends it.

        // Drive many cadence steps well past any plausible game length; without a deadline it must not stop.
        for (var i = 0; i < 200; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(10));
            await Task.Delay(5);
        }

        Assert.True(sut.IsTracking);
        await sut.StopAsync();
    }

    // Advances the fake clock a cadence at a time, yielding between steps so the loop task (spawned via
    // Task.Run) actually registers and fires its next Task.Delay before we check the condition.
    private async Task AdvanceUntilAsync(Func<bool> condition, TimeSpan step, int maxAttempts = 200)
    {
        for (var i = 0; i < maxAttempts && !condition(); i++)
        {
            _time.Advance(step);
            await Task.Delay(5);
        }
    }
}
