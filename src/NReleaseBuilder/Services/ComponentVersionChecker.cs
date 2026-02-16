using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Models;

namespace NReleaseBuilder.Services;

/// <summary>
/// Builds component check rows by comparing current and available versions.
/// </summary>
public sealed class ComponentVersionChecker : IComponentVersionChecker
{
    /// <inheritdoc />
    public IReadOnlyList<ComponentCheckRow> BuildRows(
        IReadOnlyList<ComponentRow> componentRows,
        IReadOnlyDictionary<RepositoryName, RepositoryTagLookup> tagLookups)
    {
        ArgumentNullException.ThrowIfNull(componentRows);
        ArgumentNullException.ThrowIfNull(tagLookups);

        var result = new List<ComponentCheckRow>(componentRows.Count);

        for (var i = 0; i < componentRows.Count; i++)
        {
            var row = componentRows[i];

            var repositoryName = new RepositoryName(row.Repository);

            if (!tagLookups.TryGetValue(repositoryName, out var lookup))
            {
                result.Add(new ComponentCheckRow(
                    i + 1,
                    row.Component,
                    row.Repository,
                    row.Version,
                    CheckStatus.BitbucketError,
                    "Repository lookup result is missing.",
                    []));
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
                    []));
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
                    []));
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
                    []));
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
                    []));
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
