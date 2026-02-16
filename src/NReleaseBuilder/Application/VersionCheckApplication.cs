using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using NReleaseBuilder.Presentation;
using NReleaseBuilder.Services;
using NuGet.Versioning;

namespace NReleaseBuilder.Application;

public sealed class VersionCheckApplication
{
    private readonly AppSettingsLoader _settingsLoader;
    private readonly CsvComponentReader _csvReader;
    private readonly BitbucketTagClient _bitbucketTagClient;
    private readonly ComponentVersionChecker _versionChecker;
    private readonly SpectreConsoleRenderer _renderer;

    public VersionCheckApplication()
        : this(
            new AppSettingsLoader(),
            new CsvComponentReader(),
            new BitbucketTagClient(),
            new ComponentVersionChecker(),
            new SpectreConsoleRenderer())
    {
    }

    public VersionCheckApplication(
        AppSettingsLoader settingsLoader,
        CsvComponentReader csvReader,
        BitbucketTagClient bitbucketTagClient,
        ComponentVersionChecker versionChecker,
        SpectreConsoleRenderer renderer)
    {
        _settingsLoader = settingsLoader;
        _csvReader = csvReader;
        _bitbucketTagClient = bitbucketTagClient;
        _versionChecker = versionChecker;
        _renderer = renderer;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (!_settingsLoader.TryLoad(out var settings, out var error))
        {
            _renderer.PrintError(error ?? "Unknown configuration error.");
            return 1;
        }

        IReadOnlyList<ComponentRow> componentRows;
        try
        {
            componentRows = _csvReader.Read(settings.CsvFilePath);
        }
        catch (Exception ex)
        {
            _renderer.PrintError($"Failed to parse CSV: {ex.Message}");
            return 1;
        }

        _renderer.RenderHeader(settings);

        if (componentRows.Count == 0)
        {
            _renderer.PrintNoRows();
            return 0;
        }

        var repositories = componentRows
            .Select(x => x.Repository)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var minCurrentVersionByRepository = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in componentRows)
        {
            if (!VersionParser.TryParse(row.Version, out var parsedVersion))
            {
                continue;
            }

            if (!minCurrentVersionByRepository.TryGetValue(row.Repository, out var minVersion) || parsedVersion < minVersion)
            {
                minCurrentVersionByRepository[row.Repository] = parsedVersion;
            }
        }

        _renderer.PrintRepositoryCheckCount(repositories.Length);

        Dictionary<string, RepositoryTagLookup> tagLookups;
        try
        {
            tagLookups = await _renderer
                .RunBitbucketLoadingWithProgressAsync(
                    repositories,
                    progress => _bitbucketTagClient.FetchRepositoryTagLookupsAsync(
                        repositories,
                        minCurrentVersionByRepository,
                        settings.Bitbucket,
                        settings.Jira,
                        progress,
                        cancellationToken))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _renderer.PrintError($"Failed to load tags from Bitbucket: {ex.Message}");
            return 1;
        }

        var checkRows = _versionChecker.BuildRows(componentRows, tagLookups);
        var filteredRows = FilterRowsByAllowedJiraStatuses(checkRows, settings.Jira.AllowedTaskStatuses);

        if (filteredRows.Count == 0)
        {
            _renderer.PrintNoComponentsMatchedStatusFilter(settings.Jira.AllowedTaskStatuses);
            var statusStats = BuildStatusStatistics(checkRows);
            _renderer.PrintStatusFilterDiagnostics(statusStats, settings.Jira.AllowedTaskStatuses);
            return 0;
        }

        _renderer.RenderTable(filteredRows);
        _renderer.RenderSummary(filteredRows);
        _renderer.RenderSlackCopyText(filteredRows, settings.Jira.AllowedTaskStatuses);

        return 0;
    }

    private static IReadOnlyList<ComponentCheckRow> FilterRowsByAllowedJiraStatuses(
        IReadOnlyList<ComponentCheckRow> rows,
        IReadOnlyList<string> allowedStatuses)
    {
        var allowed = new HashSet<string>(
            allowedStatuses
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Select(static x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
        {
            return Array.Empty<ComponentCheckRow>();
        }

        return rows
            .Where(row => ComponentMatchesStatusFilter(row, allowed))
            .ToArray();
    }

    private static bool ComponentMatchesStatusFilter(
        ComponentCheckRow row,
        IReadOnlySet<string> allowedStatuses)
    {
        var hasAnyTaskStatus = false;

        foreach (var newerVersion in row.NewerVersions)
        {
            var statuses = newerVersion.JiraStatus
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var status in statuses)
            {
                hasAnyTaskStatus = true;

                if (!allowedStatuses.Contains(status))
                {
                    return false;
                }
            }
        }

        return hasAnyTaskStatus;
    }

    private static IReadOnlyDictionary<string, int> BuildStatusStatistics(IReadOnlyList<ComponentCheckRow> rows)
    {
        return rows
            .SelectMany(static row => row.NewerVersions)
            .SelectMany(static version => version.JiraStatus
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static status => !string.IsNullOrWhiteSpace(status))
            .GroupBy(static status => status, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }
}
