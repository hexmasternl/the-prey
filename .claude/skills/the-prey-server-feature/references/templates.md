# Server feature slice — templates

Derived from real slices (`Games/Features/SetReady`, `Games/Features/CreateGame`, `PlayFields/Features/DeletePlayField`, `GameEndpoints.cs`, `DeletePlayFieldCommandHandlerTests.cs`). Replace `{Domain}`, `{Feature}`, `Game`/`game` with your names. The examples use the Games module.

## 1. Command + result — `Features/{Feature}/{Feature}Command.cs`

```csharp
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.{Feature};

public sealed record {Feature}Command(Guid GameId, Guid UserId /*, other inputs */);

// Single outcome: null from the handler means "not found".
public sealed record {Feature}Result(GameDto Game);

// Several outcomes (PlayFields style) — the endpoint switches on the case:
// public abstract record {Feature}Result
// {
//     public sealed record Success(GameDto Game) : {Feature}Result;
//     public sealed record NotFound : {Feature}Result;
//     public sealed record Forbidden : {Feature}Result;
// }
```

Query: `public sealed record {Feature}Query(Guid GameId, Guid RequestingUserId);` in `{Feature}Query.cs`.

## 2. Command handler — `Features/{Feature}/{Feature}CommandHandler.cs`

```csharp
using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Features.{Feature};

public sealed class {Feature}CommandHandler : ICommandHandler<{Feature}Command, {Feature}Result?>
{
    private readonly IGameRepository _games;
    private readonly ILobbyEventBus _eventBus;
    private readonly IGameMetrics _metrics;
    private readonly ILogger<{Feature}CommandHandler> _logger;

    public {Feature}CommandHandler(
        IGameRepository games,
        ILobbyEventBus eventBus,
        IGameMetrics metrics,
        ILogger<{Feature}CommandHandler> logger)
    {
        _games = games;
        _eventBus = eventBus;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<{Feature}Result?> Handle({Feature}Command command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("{Feature}");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);
            if (game is null)
                return null;

            if (!game.IsVisibleTo(command.UserId))
                throw new UnauthorizedAccessException("The caller is not a member of this game.");

            game.DoSomething(command.UserId);            // domain method enforces the rules

            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(
                game.Id,
                RealtimeProtocol.MessageTypes.ParticipantChanged,
                game.ToParticipantDto(command.UserId),
                ct);

            _metrics.RecordSomething();                  // only if you added a metric
            _logger.LogInformation("{Feature} handled for game {GameId}", game.Id);

            return new {Feature}Result(game.ToDto(command.UserId));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
```

PlayFields: `PlayFieldActivitySource.Source`, `IPlayFieldRepository`, `IPlayFieldMetrics`, tag `playfield.id`. Users: `UserActivitySource.Source`, `IUserRepository`, tag `user.id`.

## 3. Query handler

```csharp
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.{Feature};

public sealed class {Feature}QueryHandler : IQueryHandler<{Feature}Query, GameDto?>
{
    private readonly IGameRepository _games;

    public {Feature}QueryHandler(IGameRepository games) => _games = games;

    public async Task<GameDto?> Handle({Feature}Query query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var game = await _games.GetByIdAsync(query.GameId, ct);
        return game?.ToDto(query.RequestingUserId);
    }
}
```

Add the same activity/try/catch block as the command handler when the query does more than one repository read.

## 4. Metric — `Observability/IGameMetrics.cs` + `GameMetrics.cs`

```csharp
// IGameMetrics
void RecordSomething();

// GameMetrics — field, constructor, method
private readonly Counter<long> _something;

_something = meter.CreateCounter<long>(
    "games.something",
    unit: "{thing}",
    description: "Total number of things");

public virtual void RecordSomething() => _something.Add(1);
```

## 5. Request DTO — `Abstractions/DataTransferObjects/`

```csharp
namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>One-line summary. The user id is taken from the authenticated caller.</summary>
public sealed record {Feature}Request(string SomeValue, int? Optional = null);
```

## 6. Endpoint — `Api/Endpoints/GameEndpoints.cs`

Inside `MapGameEndpoints`, next to the other routes in the `/games` group:

```csharp
group.MapPost("/{id:guid}/{feature-route}", {Feature})
    .WithName("{Feature}")
    .Produces<GameDto>()
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status401Unauthorized);
```

Handler method in the same class:

```csharp
private static async Task<IResult> {Feature}(
    Guid id,
    [FromBody] {Feature}Request request,
    ClaimsPrincipal principal,
    IUserResolver userResolver,
    ICommandHandler<{Feature}Command, {Feature}Result?> handler,
    CancellationToken ct)
{
    var subjectId = principal.FindFirstValue("sub");
    if (subjectId is null) return Results.Unauthorized();
    var user = await userResolver.ResolveUser(subjectId, ct);
    if (user is null) return Results.Unauthorized();

    try
    {
        var result = await handler.Handle(new {Feature}Command(id, user.UserId), ct);
        return result is null ? Results.NotFound() : Results.Ok(result.Game);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
    catch (GameRuleException ex)
    {
        return GameRuleProblem(ex.Code, ex.Message, StatusCodes.Status409Conflict);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return ValidationProblem(ex);
    }
}
```

`ValidationProblem(Exception)` and `GameRuleProblem(code, detail, status)` are existing private helpers at the bottom of `GameEndpoints.cs`. In PlayFields the equivalent is inline `Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] })`.

Switching on a discriminated result (PlayFields `UpsertPlayField`):

```csharp
return result switch
{
    {Feature}Result.Success s => Results.Ok(s.Game),
    {Feature}Result.NotFound => Results.NotFound(),
    {Feature}Result.Forbidden => Results.Forbid(),
    _ => Results.StatusCode(500)
};
```

## 7. Rule exception with a client code — `DomainModels/GameRuleException.cs`

```csharp
/// <summary>When this happens.</summary>
public sealed class SomethingNotAllowedException : GameRuleException
{
    public override string Code => "something_not_allowed";

    public SomethingNotAllowedException() : base("Human-readable explanation.") { }
}
```

## 8. Registration — `{Domain}ModuleRegistration.cs`

```csharp
using HexMaster.ThePrey.Games.Features.{Feature};
// ...
services.AddScoped<ICommandHandler<{Feature}Command, {Feature}Result?>, {Feature}CommandHandler>();
services.AddScoped<IQueryHandler<{Feature}Query, GameDto?>, {Feature}QueryHandler>();
```

## 9. Faker addition — `Tests/Factories/GameFaker.cs`

```csharp
internal static class GameFaker
{
    private static readonly Faker _faker = new();

    // Add a focused factory for the state your feature needs; reuse existing ones where you can.
    internal static Game LobbyGameWithSomething(out Guid memberId)
    {
        var game = LobbyGameWithPlayers(2, out var ids, markReady: false);
        memberId = ids[0];
        return game;
    }
}
```

New module faker (PlayFields/Users style):

```csharp
using Bogus;
using HexMaster.ThePrey.{Domain}.DomainModels;

namespace HexMaster.ThePrey.{Domain}.Tests.Factories;

internal static class {Entity}Faker
{
    private static readonly Faker _faker = new();

    internal static {Entity} CreateValid(string? name = null, Guid? ownerId = null) =>
        {Entity}.Create(name ?? _faker.Address.City(), ownerId ?? _faker.Random.Guid());
}
```

## 10. Handler test — `Tests/Features/{Feature}/{Feature}CommandHandlerTests.cs`

```csharp
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.{Feature};
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features.{Feature};

public sealed class {Feature}CommandHandlerTests
{
    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<ILobbyEventBus> _eventBus = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly Mock<ILogger<{Feature}CommandHandler>> _logger = new();
    private readonly {Feature}CommandHandler _sut;

    public {Feature}CommandHandlerTests() =>
        _sut = new {Feature}CommandHandler(_repository.Object, _eventBus.Object, _metrics.Object, _logger.Object);

    [Fact]
    public async Task Handle_ShouldReturnUpdatedGame_WhenCallerIsMember()
    {
        // Arrange
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        // Act
        var result = await _sut.Handle(new {Feature}Command(game.Id, ids[0]), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordSomething(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _sut.Handle(new {Feature}Command(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccess_WhenCallerIsNotMember()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.Handle(new {Feature}Command(game.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenRepositoryThrows()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.Handle(new {Feature}Command(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        _metrics.Verify(m => m.RecordSomething(), Times.Never);
    }
}
```

`Xunit` is a global using in every test csproj (`<Using Include="Xunit" />`), so no `using Xunit;` is needed.

## 11. Integration event (only for a new cross-module topic)

```csharp
// server/src/Shared/HexMaster.ThePrey.IntegrationEvents/IntegrationEventTopics.cs
public const string SomethingHappened = "something-happened";

// server/src/Shared/HexMaster.ThePrey.IntegrationEvents/Events/SomethingHappenedIntegrationEvent.cs
namespace HexMaster.ThePrey.IntegrationEvents.Events;

public sealed record SomethingHappenedIntegrationEvent(Guid GameId, string Detail)
    : IntegrationEvent, IGameScopedIntegrationEvent
{
    public override string Topic => IntegrationEventTopics.SomethingHappened;
}

// Producer handler: inject IIntegrationEventPublisher (registered by services.AddIntegrationEvents())
await _publisher.PublishAsync(new SomethingHappenedIntegrationEvent(game.Id, "..."), ct);

// Consumer Api: app.UseCloudEvents() + app.MapSubscribeHandler() in Program.cs, then
app.MapPost($"/notifications/events/{IntegrationEventTopics.SomethingHappened}",
        async (SomethingHappenedIntegrationEvent e, /* services */ CancellationToken ct) => { /* ... */ return Results.Ok(); })
    .WithTopic(DaprIntegrationEventPublisher.PubSubName, IntegrationEventTopics.SomethingHappened)
    .AllowAnonymous();
```
