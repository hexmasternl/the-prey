namespace HexMaster.ThePrey.IntegrationEvents;

/// <summary>
/// Base record for integration events. Supplies a generated <see cref="Id"/> and an
/// <see cref="OccurredAt"/> timestamp; concrete events declare their <see cref="Topic"/>.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public abstract string Topic { get; }
}
