using System.Text.Json.Serialization;

namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>
/// Client-supplied app version for the version gate. The JSON key is <c>current-version</c>
/// (e.g. <c>{ "current-version": "1.2.3" }</c>) to match the contract the client posts.
/// </summary>
public sealed record CheckAppVersionRequest(
    [property: JsonPropertyName("current-version")] string? CurrentVersion);
