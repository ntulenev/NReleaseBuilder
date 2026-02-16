using System.Text.Json;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using NReleaseBuilder.Services;

using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

using NuGet.Versioning;

namespace NReleaseBuilder.Application;

/// <summary>
/// Coordinates end-to-end component version checks and console output.
/// </summary>
public sealed class VersionCheckApplication : IVersionCheckApplication
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionCheckApplication"/> class.
    /// </summary>
    /// <param name="csvReader">CSV reader service.</param>
    /// <param name="bitbucketTagClient">Bitbucket tag client.</param>
    /// <param name="versionChecker">Version comparison service.</param>
    /// <param name="renderer">Console renderer.</param>
    /// <param name="options">Application settings options.</param>
    public VersionCheckApplication(
        ICsvComponentReader csvReader,
        IBitbucketTagClient bitbucketTagClient,
        IComponentVersionChecker versionChecker,
        IConsoleRenderer renderer,
        IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(csvReader);
        ArgumentNullException.ThrowIfNull(bitbucketTagClient);
        ArgumentNullException.ThrowIfNull(versionChecker);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(options);

        _csvReader = csvReader;
        _bitbucketTagClient = bitbucketTagClient;
        _versionChecker = versionChecker;
        _renderer = renderer;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ComponentRow> componentRows;
        try
        {
            componentRows = _csvReader.Read(_settings.CsvFilePath);
        }
        catch (MalformedLineException ex)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to parse CSV: {ex.Message}"));
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to parse CSV: {ex.Message}"));
            return 1;
        }
        catch (IOException ex)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to parse CSV: {ex.Message}"));
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to parse CSV: {ex.Message}"));
            return 1;
        }
        catch (ArgumentException ex)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to parse CSV: {ex.Message}"));
            return 1;
        }

        _renderer.RenderHeader(_settings);

        if (componentRows.Count == 0)
        {
            _renderer.PrintNoRows();
            return 0;
        }

        var repositories = componentRows
            .Select(static x => new RepositoryName(x.Repository))
            .Distinct()
            .ToArray();

        var minCurrentVersionByRepository = new Dictionary<RepositoryName, NuGetVersion>();

        foreach (var row in componentRows)
        {
            if (!VersionParser.TryParse(row.Version, out var parsedVersion))
            {
                continue;
            }

            var repositoryName = new RepositoryName(row.Repository);

            if (!minCurrentVersionByRepository.TryGetValue(repositoryName, out var minVersion)
                || parsedVersion < minVersion)
            {
                minCurrentVersionByRepository[repositoryName] = parsedVersion;
            }
        }

        _renderer.PrintRepositoryCheckCount(repositories.Length);

        Dictionary<RepositoryName, RepositoryTagLookup> tagLookups;
        try
        {
            tagLookups = await _renderer
                .RunBitbucketLoadingWithProgressAsync(
                    repositories,
                    progress => _bitbucketTagClient.FetchRepositoryTagLookupsAsync(
                        repositories,
                        minCurrentVersionByRepository,
                        progress,
                        cancellationToken))
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to load tags from Bitbucket: {ex.Message}"));
            return 1;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to load tags from Bitbucket: {ex.Message}"));
            return 1;
        }
        catch (JsonException ex)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to load tags from Bitbucket: {ex.Message}"));
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            _renderer.PrintError(new ErrorMessage($"Failed to load tags from Bitbucket: {ex.Message}"));
            return 1;
        }

        var checkRows = _versionChecker.BuildRows(componentRows, tagLookups);
        var allowedStatuses = BuildAllowedStatuses(_settings.Jira.AllowedTaskStatuses);
        var filteredRows = FilterRowsByAllowedJiraStatuses(checkRows, allowedStatuses);

        if (filteredRows.Length == 0)
        {
            _renderer.PrintNoComponentsMatchedStatusFilter(allowedStatuses);
            var statusStatistics = BuildStatusStatistics(checkRows);
            _renderer.PrintStatusFilterDiagnostics(statusStatistics, allowedStatuses);
            return 0;
        }

        _renderer.RenderTable(filteredRows);
        _renderer.RenderSummary(filteredRows);
        _renderer.RenderSlackCopyText(filteredRows, allowedStatuses);

        return 0;
    }

    private static ComponentCheckRow[] FilterRowsByAllowedJiraStatuses(
        IReadOnlyList<ComponentCheckRow> rows,
        IReadOnlyList<JiraStatusName> allowedStatuses)
    {
        var allowed = new HashSet<JiraStatusName>(allowedStatuses);

        if (allowed.Count == 0)
        {
            return [];
        }

        return [.. rows.Where(row => ComponentMatchesStatusFilter(row, allowed))];
    }

    private static bool ComponentMatchesStatusFilter(
        ComponentCheckRow row,
        HashSet<JiraStatusName> allowedStatuses)
    {
        var hasAnyTaskStatus = false;

        foreach (var newerVersion in row.NewerVersions)
        {
            var statuses = newerVersion.JiraStatus
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var status in statuses)
            {
                if (!JiraStatusName.TryCreate(status, out var jiraStatus))
                {
                    continue;
                }

                hasAnyTaskStatus = true;

                if (!allowedStatuses.Contains(jiraStatus))
                {
                    return false;
                }
            }
        }

        return hasAnyTaskStatus;
    }

    private static Dictionary<JiraStatusName, int> BuildStatusStatistics(
        IReadOnlyList<ComponentCheckRow> rows)
    {
        var statistics = new Dictionary<JiraStatusName, int>();

        foreach (var row in rows)
        {
            foreach (var version in row.NewerVersions)
            {
                var statuses = version.JiraStatus
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var status in statuses)
                {
                    if (!JiraStatusName.TryCreate(status, out var jiraStatus))
                    {
                        continue;
                    }

                    _ = statistics.TryGetValue(jiraStatus, out var currentCount);
                    statistics[jiraStatus] = currentCount + 1;
                }
            }
        }

        return statistics;
    }

    private static JiraStatusName[] BuildAllowedStatuses(IReadOnlyList<string> configuredAllowedStatuses)
    {
        ArgumentNullException.ThrowIfNull(configuredAllowedStatuses);

        return
        [
            .. configuredAllowedStatuses
                .Select(static status => new JiraStatusName(status))
                .Distinct()
        ];
    }

    private readonly ICsvComponentReader _csvReader;
    private readonly IBitbucketTagClient _bitbucketTagClient;
    private readonly IComponentVersionChecker _versionChecker;
    private readonly IConsoleRenderer _renderer;
    private readonly AppSettings _settings;
}
