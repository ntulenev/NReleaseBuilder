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
    /// <param name="jiraStatusStatisticsBuilder">Jira status statistics builder.</param>
    /// <param name="renderer">Console renderer.</param>
    /// <param name="options">Application settings options.</param>
    public VersionCheckApplication(
        ICsvComponentReader csvReader,
        IBitbucketTagClient bitbucketTagClient,
        IComponentVersionChecker versionChecker,
        IJiraStatusStatisticsBuilder jiraStatusStatisticsBuilder,
        IConsoleRenderer renderer,
        IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(csvReader);
        ArgumentNullException.ThrowIfNull(bitbucketTagClient);
        ArgumentNullException.ThrowIfNull(versionChecker);
        ArgumentNullException.ThrowIfNull(jiraStatusStatisticsBuilder);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(options);

        _csvReader = csvReader;
        _bitbucketTagClient = bitbucketTagClient;
        _versionChecker = versionChecker;
        _jiraStatusStatisticsBuilder = jiraStatusStatisticsBuilder;
        _renderer = renderer;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var componentRows = TryReadComponentRows();

        if (componentRows is null)
        {
            return 1;
        }

        _renderer.RenderHeader(_settings);

        if (TryHandleNoComponentRows(componentRows))
        {
            return 0;
        }

        var repositoryContext = BuildRepositoryVersionContext(componentRows);
        _renderer.PrintRepositoryCheckCount(repositoryContext.Repositories.Length);

        var tagLookups = await TryLoadTagLookupsAsync(repositoryContext, cancellationToken)
            .ConfigureAwait(false);

        if (tagLookups is null)
        {
            return 1;
        }

        var checkRows = _versionChecker.BuildRows(componentRows, tagLookups);
        var allowedStatuses = _settings.Jira.BuildAllowedStatuses();
        var filteredRows = FilterRowsByAllowedJiraStatuses(checkRows, allowedStatuses);

        return RenderResults(checkRows, filteredRows, allowedStatuses);
    }

    private IReadOnlyList<ComponentRow>? TryReadComponentRows()
    {
        try
        {
            return _csvReader.Read(_settings.CsvFilePath);
        }
        catch (MalformedLineException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (IOException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (ArgumentException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
    }

    private bool TryHandleNoComponentRows(IReadOnlyList<ComponentRow> componentRows)
    {
        if (componentRows.Count != 0)
        {
            return false;
        }

        _renderer.PrintNoRows();
        return true;
    }

    private static RepositoryVersionContext BuildRepositoryVersionContext(IReadOnlyList<ComponentRow> componentRows)
    {
        var repositories = componentRows
            .Select(static row => row.Repository)
            .Distinct()
            .ToArray();

        var minCurrentVersionsByRepository = BuildMinCurrentVersionsByRepository(componentRows);

        return new RepositoryVersionContext(repositories, minCurrentVersionsByRepository);
    }

    private static Dictionary<RepositoryName, NuGetVersion> BuildMinCurrentVersionsByRepository(
        IReadOnlyList<ComponentRow> componentRows)
    {
        var minCurrentVersionsByRepository = new Dictionary<RepositoryName, NuGetVersion>();

        foreach (var row in componentRows)
        {
            if (!VersionParser.TryParse(row.Version, out var parsedVersion))
            {
                continue;
            }

            var repositoryName = row.Repository;

            if (!minCurrentVersionsByRepository.TryGetValue(repositoryName, out var minVersion)
                || parsedVersion < minVersion)
            {
                minCurrentVersionsByRepository[repositoryName] = parsedVersion;
            }
        }

        return minCurrentVersionsByRepository;
    }

    private async Task<Dictionary<RepositoryName, RepositoryTagLookup>?> TryLoadTagLookupsAsync(
        RepositoryVersionContext repositoryContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var repositories = repositoryContext.Repositories;
            var lookupsByRepository = new Dictionary<RepositoryName, RepositoryTagLookup>(repositories.Length);
            var repositoryBatches = repositories
                .Chunk(BITBUCKET_REPOSITORY_BATCH_SIZE)
                .ToArray();

            for (var batchIndex = 0; batchIndex < repositoryBatches.Length; batchIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchRepositories = repositoryBatches[batchIndex];
                _renderer.PrintRepositoryBatchProgress(
                    batchIndex + 1,
                    repositoryBatches.Length,
                    lookupsByRepository.Count,
                    batchRepositories.Length,
                    repositories.Length);

                var batchLookups = await _renderer
                    .RunBitbucketLoadingWithProgressAsync(
                        batchRepositories,
                        progress => _bitbucketTagClient.FetchRepositoryTagLookupsAsync(
                            batchRepositories,
                            repositoryContext.MinCurrentVersionsByRepository,
                            progress,
                            cancellationToken))
                    .ConfigureAwait(false);

                foreach (var (repository, lookup) in batchLookups)
                {
                    lookupsByRepository[repository] = lookup;
                }
            }

            return lookupsByRepository;
        }
        catch (HttpRequestException ex)
        {
            PrintBitbucketLoadingError(ex);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            PrintBitbucketLoadingError(ex);
            return null;
        }
        catch (JsonException ex)
        {
            PrintBitbucketLoadingError(ex);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            PrintBitbucketLoadingError(ex);
            return null;
        }
    }

    private int RenderResults(
        IReadOnlyList<ComponentCheckRow> checkRows,
        ComponentCheckRow[] filteredRows,
        IReadOnlyList<JiraStatusName> allowedStatuses)
    {
        if (filteredRows.Length == 0)
        {
            _renderer.PrintNoComponentsMatchedStatusFilter(allowedStatuses);
            var statusStatistics = _jiraStatusStatisticsBuilder.Build(checkRows);
            _renderer.PrintStatusFilterDiagnostics(statusStatistics, allowedStatuses);
            return 0;
        }

        _renderer.RenderTable(filteredRows);
        _renderer.RenderSummary(filteredRows);
        _renderer.RenderSlackCopyText(filteredRows, allowedStatuses);

        return 0;
    }

    private void PrintCsvParsingError(Exception exception)
        => _renderer.PrintError(new ErrorMessage($"Failed to parse CSV: {exception.Message}"));

    private void PrintBitbucketLoadingError(Exception exception)
        => _renderer.PrintError(new ErrorMessage($"Failed to load tags from Bitbucket: {exception.Message}"));

    private static ComponentCheckRow[] FilterRowsByAllowedJiraStatuses(
        IReadOnlyList<ComponentCheckRow> rows,
        IReadOnlyList<JiraStatusName> allowedStatuses)
    {
        var allowed = new HashSet<JiraStatusName>(allowedStatuses);

        return allowed.Count == 0 ? [] : [.. rows.Where(row => row.MatchesStatusFilter(allowed))];
    }

    private readonly record struct RepositoryVersionContext(
        RepositoryName[] Repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> MinCurrentVersionsByRepository);

    private const int BITBUCKET_REPOSITORY_BATCH_SIZE = 10;
    private readonly ICsvComponentReader _csvReader;
    private readonly IBitbucketTagClient _bitbucketTagClient;
    private readonly IComponentVersionChecker _versionChecker;
    private readonly IJiraStatusStatisticsBuilder _jiraStatusStatisticsBuilder;
    private readonly IConsoleRenderer _renderer;
    private readonly AppSettings _settings;
}
