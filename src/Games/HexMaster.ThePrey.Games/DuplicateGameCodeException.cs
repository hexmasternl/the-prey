namespace HexMaster.ThePrey.Games;

/// <summary>
/// Thrown by <see cref="IGameRepository.AddAsync"/> when the game's code collides with the code of an
/// already-persisted game. Callers regenerate the code and retry.
/// </summary>
public sealed class DuplicateGameCodeException : Exception
{
    public DuplicateGameCodeException(string gameCode, Exception? innerException = null)
        : base($"A game with code '{gameCode}' already exists.", innerException)
        => GameCode = gameCode;

    public string GameCode { get; }
}
