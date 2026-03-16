using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Application;
using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Csv;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Rendering;

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
    /// <param name="repositoryNameNormalizer">Component-row repository name normalizer.</param>
    /// <param name="componentsVersionBuilder">Component version rows builder.</param>
    /// <param name="renderer">Application renderer.</param>
    /// <param name="options">Application settings options.</param>
    public VersionCheckApplication(
        ICsvComponentReader csvReader,
        IRepositoryNameNormalizer repositoryNameNormalizer,
        IComponentsVersionBuilder componentsVersionBuilder,
        IRenderer renderer,
        IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(csvReader);
        ArgumentNullException.ThrowIfNull(repositoryNameNormalizer);
        ArgumentNullException.ThrowIfNull(componentsVersionBuilder);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(options);

        _csvReader = csvReader;
        _repositoryNameNormalizer = repositoryNameNormalizer;
        _componentsVersionBuilder = componentsVersionBuilder;
        _renderer = renderer;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        _renderer.RenderHeader();
        await RunReportRunsAsync(_settings.BuildReportRuns(), cancellationToken).ConfigureAwait(false);
        RenderComponentSourceDifferences();
        return 0;
    }

    private async Task RunReportRunsAsync(
        IReadOnlyList<ReportRunDefinition> reportRuns,
        CancellationToken cancellationToken)
    {
        for (var runIndex = 0; runIndex < reportRuns.Count; runIndex++)
        {
            var reportRun = reportRuns[runIndex];
            await RunReportRunAsync(
                reportRun,
                runIndex,
                reportRuns.Count,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunReportRunAsync(
        ReportRunDefinition reportRun,
        int runIndex,
        int totalRunCount,
        CancellationToken cancellationToken)
    {
        if (totalRunCount > 1 && !string.IsNullOrWhiteSpace(reportRun.Name))
        {
            _renderer.PrintRunHeading(reportRun.Name, runIndex, totalRunCount);
        }

        _renderer.SetupContext(reportRun);

        try
        {
            var componentRows = TryReadComponentRows(reportRun.ComponentNamesFilter)?.ToList();

            if (componentRows is null)
            {
                return;
            }

            if (TryHandleNoComponentRows(componentRows, reportRun.Name))
            {
                return;
            }

            var normalizedComponentRows = _repositoryNameNormalizer.Normalize(componentRows);
            var repositoryContext = RepositoryVersionContext.BuildRepositoryVersionContext(normalizedComponentRows);

            _renderer.PrintRepositoryCheckCount(repositoryContext.Repositories.Count);

            var checkRows = await _componentsVersionBuilder.BuildAsync(
                normalizedComponentRows,
                repositoryContext,
                cancellationToken)
                .ConfigureAwait(false);

            if (checkRows is null)
            {
                return;
            }

            _renderer.RenderResults(checkRows);
        }
        finally
        {
            _renderer.ResetContext();
        }
    }

    private IReadOnlyList<ComponentRow>? TryReadComponentRows(IReadOnlyList<string>? componentNamesFilter)
        => _csvReader.Read(componentNamesFilter);

    private void RenderComponentSourceDifferences()
    {
        var sourceSnapshot = _csvReader.ReadSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return;
        }

        var differenceRows = BuildComponentSourceDifferenceRows(sourceSnapshot, _settings);
        if (differenceRows.Count == 0)
        {
            return;
        }

        _renderer.PrintComponentSourceDifferences(differenceRows);
    }

    private static List<ComponentSourceDifferenceRow> BuildComponentSourceDifferenceRows(
        ComponentSourceSnapshot sourceSnapshot,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(sourceSnapshot);
        ArgumentNullException.ThrowIfNull(settings);

        var devComponents = sourceSnapshot.DevComponents
            .Select(static x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetComponents = sourceSnapshot.TargetComponents
            .Select(static x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configuredComponents = BuildConfiguredComponentNames(settings);

        var allComponents = devComponents
            .Concat(targetComponents)
            .Concat(configuredComponents)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<ComponentSourceDifferenceRow>(allComponents.Length);

        foreach (var componentName in allComponents)
        {
            var isInDev = devComponents.Contains(componentName);
            var isInTarget = targetComponents.Contains(componentName);
            var isInSettings = configuredComponents.Contains(componentName);

            if (isInDev == isInTarget && isInTarget == isInSettings)
            {
                continue;
            }

            rows.Add(new ComponentSourceDifferenceRow(
                new ComponentName(componentName),
                isInDev,
                isInTarget,
                isInSettings));
        }

        return rows;
    }

    private static HashSet<string> BuildConfiguredComponentNames(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configuredComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (settings.CsvComponentGroups.Count > 0)
        {
            foreach (var componentName in settings.CsvComponentGroups.SelectMany(static x => x.ComponentNames))
            {
                AddConfiguredComponentName(configuredComponents, componentName);
            }

            return configuredComponents;
        }

        foreach (var componentName in settings.CsvComponentNamesFilter)
        {
            AddConfiguredComponentName(configuredComponents, componentName);
        }

        return configuredComponents;
    }

    private static void AddConfiguredComponentName(HashSet<string> configuredComponents, string? componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            return;
        }

        _ = configuredComponents.Add(componentName.Trim());
    }

    private bool TryHandleNoComponentRows(List<ComponentRow> componentRows, string? runName)
    {
        if (componentRows.Count != 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(runName))
        {
            _renderer.PrintNoRowsMatchedGroup(runName);
        }

        _renderer.PrintNoRows();
        return true;
    }

    private readonly ICsvComponentReader _csvReader;
    private readonly IRepositoryNameNormalizer _repositoryNameNormalizer;
    private readonly IComponentsVersionBuilder _componentsVersionBuilder;
    private readonly IRenderer _renderer;
    private readonly AppSettings _settings;

}
