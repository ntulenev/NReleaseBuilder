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
