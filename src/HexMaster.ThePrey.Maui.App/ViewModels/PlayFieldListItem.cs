using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>Which tab of the playfields list page is active.</summary>
public enum PlayFieldsTab
{
    Private,
    Public
}

/// <summary>
/// A single row in a playfields list: the playfield's name plus its visibility, from which the page
/// renders a <c>PUBLIC</c>/<c>PRIVATE</c> badge. <see cref="IsPublic"/>/<see cref="IsPrivate"/> drive
/// which localized, coloured badge label the item template shows.
/// </summary>
public sealed class PlayFieldListItem
{
    public PlayFieldListItem(PlayFieldSummary summary)
    {
        Id = summary.Id;
        Name = summary.Name;
        IsPublic = summary.IsPublic;
    }

    /// <summary>The playfield's identifier, so a removal targets the exact backend record.</summary>
    public Guid Id { get; }

    public string Name { get; }

    public bool IsPublic { get; }

    public bool IsPrivate => !IsPublic;
}
