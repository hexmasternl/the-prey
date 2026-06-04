using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

/// <summary>
/// Observable state of the running game session, maintained by <see cref="GameEngineService"/> and
/// bound to by game-session pages. A DI singleton; all change notifications are dispatched on the
/// main thread so bindings can consume them directly.
/// </summary>
public sealed class GameStateContext : INotifyPropertyChanged
{
    private readonly object _gate = new();

    private bool _isRunning;
    private PlayerRole _playerRole;
    private bool _gpsAvailable = true;
    private DateTimeOffset? _lastLocationPushedAt;
    private DateTimeOffset? _lastStateSyncAt;
    private int _consecutiveErrors;
    private bool _gameEnded;
    private int? _hunterDistanceMeters;
    private bool _isUnderPenalty;
    private DateTimeOffset? _penaltyEndsAt;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>True while the game engine loops are active.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    /// <summary>The local player's role in the current session.</summary>
    public PlayerRole PlayerRole
    {
        get => _playerRole;
        set => SetProperty(ref _playerRole, value);
    }

    /// <summary>False when no GPS fix (current or cached) could be obtained on the last attempt.</summary>
    public bool GpsAvailable
    {
        get => _gpsAvailable;
        set => SetProperty(ref _gpsAvailable, value);
    }

    /// <summary>UTC moment of the last successful location push.</summary>
    public DateTimeOffset? LastLocationPushedAt
    {
        get => _lastLocationPushedAt;
        set => SetProperty(ref _lastLocationPushedAt, value);
    }

    /// <summary>UTC moment of the last successful game-state sync.</summary>
    public DateTimeOffset? LastStateSyncAt
    {
        get => _lastStateSyncAt;
        set => SetProperty(ref _lastStateSyncAt, value);
    }

    /// <summary>Number of consecutive failed location-push cycles (reset on success).</summary>
    public int ConsecutiveErrors
    {
        get => _consecutiveErrors;
        set => SetProperty(ref _consecutiveErrors, value);
    }

    /// <summary>True once the server reported the game as not found / ended.</summary>
    public bool GameEnded
    {
        get => _gameEnded;
        set => SetProperty(ref _gameEnded, value);
    }

    /// <summary>Prey only: distance to the hunter in meters; null while the hunter has no known location.</summary>
    public int? HunterDistanceMeters
    {
        get => _hunterDistanceMeters;
        set => SetProperty(ref _hunterDistanceMeters, value);
    }

    /// <summary>Hunter only: the most recently reported prey positions. Replaced on the main thread.</summary>
    public ObservableCollection<GameCoordinate> PreyLocations { get; } = [];

    /// <summary>True while the server has an active penalty override for this player.</summary>
    public bool IsUnderPenalty
    {
        get => _isUnderPenalty;
        set => SetProperty(ref _isUnderPenalty, value);
    }

    /// <summary>The moment the active penalty expires; null when no penalty is active.</summary>
    public DateTimeOffset? PenaltyEndsAt
    {
        get => _penaltyEndsAt;
        set => SetProperty(ref _penaltyEndsAt, value);
    }

    /// <summary>Replaces <see cref="PreyLocations"/> with the given coordinates on the main thread.</summary>
    public void ReplacePreyLocations(IReadOnlyList<GameCoordinate> coordinates)
    {
        RunOnMainThread(() =>
        {
            PreyLocations.Clear();
            foreach (var coordinate in coordinates)
                PreyLocations.Add(coordinate);
        });
    }

    /// <summary>Resets all per-session state; called when a new game session starts.</summary>
    public void Reset(PlayerRole role)
    {
        PlayerRole = role;
        GpsAvailable = true;
        LastLocationPushedAt = null;
        LastStateSyncAt = null;
        ConsecutiveErrors = 0;
        GameEnded = false;
        HunterDistanceMeters = null;
        IsUnderPenalty = false;
        PenaltyEndsAt = null;
        ReplacePreyLocations([]);
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        lock (_gate)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
        }

        RunOnMainThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }

    private static void RunOnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
            action();
        else
            MainThread.BeginInvokeOnMainThread(action);
    }
}
