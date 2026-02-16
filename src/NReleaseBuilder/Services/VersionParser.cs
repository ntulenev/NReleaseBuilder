using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace NReleaseBuilder.Services;

public static class VersionParser
{
    private static readonly Regex VersionPattern = new(@"\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?", RegexOptions.Compiled);

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

        var match = VersionPattern.Match(candidate);
        if (match.Success && NuGetVersion.TryParse(match.Value, out parsedVersion) && parsedVersion is not null)
        {
            version = parsedVersion;
            return true;
        }

        return false;
    }
}
