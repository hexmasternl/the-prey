namespace HexMaster.ThePrey.Maui.App.Services.Platform;

/// <summary>
/// Marshals work onto the UI thread. Real-time game events arrive on the Web PubSub receive loop's
/// background thread, but the view models they drive raise property-changed notifications, mutate bound
/// <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>s, and trigger Shell navigation —
/// all of which must happen on the UI thread. Behind an interface so view models stay free of MAUI
/// platform types and remain unit-testable.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Runs <paramref name="action"/> on the UI thread — inline when already on it.</summary>
    void Dispatch(Action action);
}
