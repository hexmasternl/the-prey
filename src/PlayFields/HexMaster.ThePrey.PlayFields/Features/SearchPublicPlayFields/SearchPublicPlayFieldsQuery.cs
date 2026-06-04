namespace HexMaster.ThePrey.PlayFields.Features.SearchPublicPlayFields;

public sealed record SearchPublicPlayFieldsQuery(string SearchText)
{
    /// <summary>Minimum search length; matches the client-side minimum.</summary>
    public const int MinimumSearchLength = 3;
}
