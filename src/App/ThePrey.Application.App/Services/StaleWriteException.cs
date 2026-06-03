using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

public sealed class StaleWriteException(Playfield serverCopy)
    : Exception("The server rejected the write because a newer version already exists.")
{
    public Playfield ServerCopy { get; } = serverCopy;
}
