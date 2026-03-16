using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Bitbucket.Internal;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Bitbucket;

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
            var displayCurrentVersion = row.IsReleased ? row.Version : VersionLabel.CreateNotReleasedYet();

            if (lookup.IsRepositoryMissing)
            {
                result.Add(new ComponentCheckRow(
                    new ComponentCheckIndex(i + 1),
                    row.Component,
                    resolvedRepositoryName,
                    displayCurrentVersion,
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
                    displayCurrentVersion,
                    CheckStatus.BitbucketError,
                    new RowDetails(lookup.Error),
                    []));
                continue;
            }

            var hasCurrentVersion = VersionParser.TryParse(row.Version, out var currentVersion);

            if (row.IsReleased && !hasCurrentVersion)
            {
                result.Add(new ComponentCheckRow(
                    new ComponentCheckIndex(i + 1),
                    row.Component,
                    resolvedRepositoryName,
                    displayCurrentVersion,
                    CheckStatus.InvalidCurrentVersion,
                    new RowDetails("Current version is not a valid tag format."),
                    []));
                continue;
            }

            var newerVersions = lookup.Tags
                .Select(tag => (Tag: tag, IsValid: VersionParser.TryParse(tag.Name, out var parsed), Parsed: parsed))
                .Where(x => x.IsValid && (!row.IsReleased || (hasCurrentVersion && x.Parsed > currentVersion)))
                .OrderBy(x => x.Parsed)
                .ThenBy(x => x.Tag.Name.Value, StringComparer.OrdinalIgnoreCase)
                .Select(x => new VersionJiraRow(
                    x.Tag.Name,
                    x.Tag.JiraTask,
                    x.Tag.JiraTitle,
                    x.Tag.JiraStatus,
                    x.Tag.TaskAlertDetails,
                    x.Tag.HasRequiredActions,
                    x.Tag.HasBreakingChanges,
                    x.Tag.HasDependencyIssues,
                    x.Tag.PullRequestUrl))
                .ToArray();

            if (newerVersions.Length == 0)
            {
                result.Add(new ComponentCheckRow(
                    new ComponentCheckIndex(i + 1),
                    row.Component,
                    resolvedRepositoryName,
                    displayCurrentVersion,
                    CheckStatus.UpToDate,
                    RowDetails.CreatePlaceholder(),
                    []));
                continue;
            }

            result.Add(new ComponentCheckRow(
                new ComponentCheckIndex(i + 1),
                row.Component,
                resolvedRepositoryName,
                displayCurrentVersion,
                CheckStatus.Outdated,
                RowDetails.CreatePlaceholder(),
                newerVersions));
        }

        return result;
    }
}
