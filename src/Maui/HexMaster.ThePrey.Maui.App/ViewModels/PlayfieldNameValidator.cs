using System.Text.RegularExpressions;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// The single source of truth for the "can this playfield be made public?" name rule. A name is
/// publishable when, trimmed, it splits on <c>,</c> into exactly three non-empty parts and the first
/// part is a country code of 2–3 uppercase letters (e.g. <c>NL, Amsterdam, City park</c>). Used both to
/// enable the Public/Private toggle and to force it back to Private when the name stops matching.
/// Pure and MAUI-free so it is trivially unit-testable.
/// </summary>
public static partial class PlayfieldNameValidator
{
    [GeneratedRegex("^[A-Z]{2,3}$")]
    private static partial Regex CountryCodeRegex();

    /// <summary>
    /// Returns <c>true</c> only when <paramref name="name"/> has exactly three comma-separated,
    /// non-empty parts and the first (trimmed) part is a 2–3 letter uppercase country code.
    /// </summary>
    public static bool IsPublishable(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var parts = name.Split(',');
        if (parts.Length != 3)
            return false;

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                return false;
        }

        return CountryCodeRegex().IsMatch(parts[0].Trim());
    }
}
