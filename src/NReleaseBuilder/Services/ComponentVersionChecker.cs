using NReleaseBuilder.Models;

namespace NReleaseBuilder.Services;

public sealed class ComponentVersionChecker
{
    public IReadOnlyList<ComponentCheckRow> BuildRows(
        IReadOnlyList<ComponentRow> componentRows,
        IReadOnlyDictionary<string, RepositoryTagLookup> tagLookups)
    {
        var result = new List<ComponentCheckRow>(componentRows.Count);

        for (var i = 0; i < componentRows.Count; i++)
        {
            var row = componentRows[i];

            if (!tagLookups.TryGetValue(row.Repository, out var lookup))
            {
                result.Add(new ComponentCheckRow(
                    i + 1,
                    row.Component,
                    row.Repository,
                    row.Version,
                    CheckStatus.BitbucketError,
                    "Repository lookup result is missing.",
                    Array.Empty<VersionJiraRow>()));
                continue;
            }

            if (lookup.IsRepositoryMissing)
            {
                result.Add(new ComponentCheckRow(
                    i + 1,
                    row.Component,
                    row.Repository,
                    row.Version,
                    CheckStatus.RepositoryNotFound,
                    "Repository was not found in Bitbucket workspace.",
                    Array.Empty<VersionJiraRow>()));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(lookup.Error))
            {
                result.Add(new ComponentCheckRow(
                    i + 1,
                    row.Component,
                    row.Repository,
                    row.Version,
                    CheckStatus.BitbucketError,
                    lookup.Error,
                    Array.Empty<VersionJiraRow>()));
                continue;
            }

            if (!VersionParser.TryParse(row.Version, out var currentVersion))
            {
                result.Add(new ComponentCheckRow(
                    i + 1,
                    row.Component,
                    row.Repository,
                    row.Version,
                    CheckStatus.InvalidCurrentVersion,
                    "Current version is not a valid tag format.",
                    Array.Empty<VersionJiraRow>()));
                continue;
            }

            var newerVersions = lookup.Tags
                .Select(tag => (Tag: tag, IsValid: VersionParser.TryParse(tag.Name, out var parsed), Parsed: parsed))
                .Where(x => x.IsValid && x.Parsed > currentVersion)
                .OrderBy(x => x.Parsed)
                .ThenBy(x => x.Tag.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new VersionJiraRow(x.Tag.Name, x.Tag.JiraTask, x.Tag.JiraStatus))
                .Distinct()
                .ToArray();

            if (newerVersions.Length == 0)
            {
                result.Add(new ComponentCheckRow(
                    i + 1,
                    row.Component,
                    row.Repository,
                    row.Version,
                    CheckStatus.UpToDate,
                    "-",
                    Array.Empty<VersionJiraRow>()));
                continue;
            }

            result.Add(new ComponentCheckRow(
                i + 1,
                row.Component,
                row.Repository,
                row.Version,
                CheckStatus.Outdated,
                string.Empty,
                newerVersions));
        }

        return result;
    }
}
