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

            var repositoryName = row.Repository;

            if (!tagLookups.TryGetValue(repositoryName, out var lookup))
            {
                result.Add(new ComponentCheckRow(
                    new ComponentCheckIndex(i + 1),
                    row.Component,
                    row.Repository,
                    row.Version,
                    CheckStatus.BitbucketError,
                    new RowDetails("Repository lookup result is missing."),
                    []));
                continue;
            }

            var resolvedRepositoryName = lookup.ResolvedRepository;

            if (lookup.IsRepositoryMissing)
            {
                result.Add(new ComponentCheckRow(
                    new ComponentCheckIndex(i + 1),
                    row.Component,
                    resolvedRepositoryName,
                    row.Version,
                    CheckStatus.RepositoryNotFound,
                    new RowDetails("Repository was not found in Bitbucket workspace."),
                    []));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(lookup.Error))
            {
                result.Add(new ComponentCheckRow(
                    new ComponentCheckIndex(i + 1),
                    row.Component,
                    resolvedRepositoryName,
                    row.Version,
                    CheckStatus.BitbucketError,
                    new RowDetails(lookup.Error),
                    []));
                continue;
            }

            if (!VersionParser.TryParse(row.Version, out var currentVersion))
            {
                result.Add(new ComponentCheckRow(
                    new ComponentCheckIndex(i + 1),
                    row.Component,
                    resolvedRepositoryName,
                    row.Version,
                    CheckStatus.InvalidCurrentVersion,
                    new RowDetails("Current version is not a valid tag format."),
                    []));
                continue;
            }

            var newerVersions = lookup.Tags
                .Select(tag => (Tag: tag, IsValid: VersionParser.TryParse(tag.Name, out var parsed), Parsed: parsed))
                .Where(x => x.IsValid && x.Parsed > currentVersion)
                .OrderBy(x => x.Parsed)
                .ThenBy(x => x.Tag.Name.Value, StringComparer.OrdinalIgnoreCase)
                .Select(x => new VersionJiraRow(
                    x.Tag.Name,
                    x.Tag.JiraTask,
                    x.Tag.JiraStatus,
                    x.Tag.HasRequiredActions,
                    x.Tag.HasBreakingChanges))
                .Distinct()
                .ToArray();

            if (newerVersions.Length == 0)
            {
                result.Add(new ComponentCheckRow(
                    new ComponentCheckIndex(i + 1),
                    row.Component,
                    resolvedRepositoryName,
                    row.Version,
                    CheckStatus.UpToDate,
                    new RowDetails("-"),
                    []));
                continue;
            }

            result.Add(new ComponentCheckRow(
                new ComponentCheckIndex(i + 1),
                row.Component,
                resolvedRepositoryName,
                row.Version,
                CheckStatus.Outdated,
                new RowDetails("-"),
                newerVersions));
        }

        return result;
    }
}
