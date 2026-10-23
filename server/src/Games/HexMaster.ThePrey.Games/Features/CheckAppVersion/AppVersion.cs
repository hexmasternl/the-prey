using System.Globalization;

namespace HexMaster.ThePrey.Games.Features.CheckAppVersion;

/// <summary>
/// Parses a <c>major.minor.patch</c> version string into a numeric tuple so versions compare
/// numerically (e.g. <c>1.10.0</c> &gt; <c>1.9.0</c>) rather than lexically. Tolerates 1–3
/// numeric components (missing components default to 0) and strips any semVer pre-release/build
/// suffix (the part after a <c>-</c> or <c>+</c>). Anything else is rejected.
/// </summary>
internal static class AppVersion
{
    public static bool TryParse(string? value, out (int Major, int Minor, int Patch) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Drop semVer pre-release/build metadata (1.2.3-beta+build → 1.2.3).
        var core = value.Trim();
        var cut = core.IndexOfAny(['-', '+']);
        if (cut >= 0)
        {
            core = core[..cut];
        }

        var parts = core.Split('.');
        if (parts.Length is 0 or > 3)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            // NumberStyles.None rejects signs, decimals and whitespace, so only plain digits pass.
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            numbers[i] = number;
        }

        version = (numbers[0], numbers[1], numbers[2]);
        return true;
    }
}
