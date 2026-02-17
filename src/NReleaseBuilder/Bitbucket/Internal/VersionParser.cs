using System.Text.RegularExpressions;

using NReleaseBuilder.Models;

using NuGet.Versioning;

namespace NReleaseBuilder.Bitbucket.Internal;

/// <summary>
/// Parses semantic version values from tags and free-form text.
/// </summary>
public static partial class VersionParser
{
    /// <summary>
    /// Tries to parse a <see cref="NuGetVersion"/> from a domain version label.
    /// </summary>
    /// <param name="value">Domain version label.</param>
    /// <param name="version">Parsed version when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(VersionLabel value, out NuGetVersion version) => TryParse(value.Value, out version);

    /// <summary>
    /// Tries to parse a <see cref="NuGetVersion"/> from a tag value.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <param name="version">Parsed version when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string value, out NuGetVersion version)
    {
        version = new NuGetVersion(0, 0, 0);

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.StartsWith('v') && candidate.Length > 1 && char.IsDigit(candidate[1]))
        {
            candidate = candidate[1..];
        }

        if (NuGetVersion.TryParse(candidate, out var parsedVersion) && parsedVersion is not null)
        {
            version = parsedVersion;
            return true;
        }

        var match = VersionPatternRegex().Match(candidate);
        if (match.Success && NuGetVersion.TryParse(match.Value, out parsedVersion) && parsedVersion is not null)
        {
            version = parsedVersion;
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?", RegexOptions.Compiled)]
    private static partial Regex VersionPatternRegex();
}
