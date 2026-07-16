namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Client-side projection of the backend <c>PlayFieldSummaryDto</c>. Only the fields the list page
/// renders are kept — <c>OwnerId</c>, <c>LastUpdatedOn</c>, and <c>CenterCoordinates</c> are not needed
/// to show a name + <c>PUBLIC</c>/<c>PRIVATE</c> badge. Deserialized from the backend's camelCase JSON
/// via the default (case-insensitive) web options.
/// </summary>
public sealed record PlayFieldSummary(Guid Id, string Name, bool IsPublic);
