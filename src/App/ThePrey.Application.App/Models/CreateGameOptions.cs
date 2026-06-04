namespace ThePrey.Application.App.Models;

/// <summary>
/// The user's choices on the Start Game view. All timing values are in minutes as picked;
/// <see cref="Services.GameService"/> converts the location intervals to the seconds the API expects.
/// </summary>
public sealed class CreateGameOptions
{
    public Guid PlayfieldId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public int GameDurationMinutes { get; set; }
    public int HunterDelayMinutes { get; set; }
    public int FinalStageMinutes { get; set; }
    public int DefaultLocationIntervalMinutes { get; set; }
    public int FinalLocationIntervalMinutes { get; set; }
    public bool EnablePreyBoundaryPenalty { get; set; }
    public bool EnableHunterBoundaryPenalty { get; set; }
}
