using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Presentation;

/// <summary>
/// Facade renderer that orchestrates console and PDF rendering.
/// </summary>
public sealed class GeneralFacadeRenderer : IRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralFacadeRenderer"/> class.
    /// </summary>
    /// <param name="consoleRenderer">Console renderer.</param>
    /// <param name="excelReportRenderer">Excel report renderer.</param>
    /// <param name="pdfReportRenderer">PDF report renderer.</param>
    /// <param name="jiraStatusStatisticsBuilder">Jira status statistics builder.</param>
    /// <param name="options">Application settings options.</param>
    public GeneralFacadeRenderer(
        IConsoleOutputRenderer consoleRenderer,
        IExcelReportRenderer excelReportRenderer,
        IPdfReportRenderer pdfReportRenderer,
        IJiraStatusStatisticsBuilder jiraStatusStatisticsBuilder,
        IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(consoleRenderer);
        ArgumentNullException.ThrowIfNull(excelReportRenderer);
        ArgumentNullException.ThrowIfNull(pdfReportRenderer);
        ArgumentNullException.ThrowIfNull(jiraStatusStatisticsBuilder);
        ArgumentNullException.ThrowIfNull(options);

        _consoleRenderer = consoleRenderer;
        _excelReportRenderer = excelReportRenderer;
        _pdfReportRenderer = pdfReportRenderer;
        _jiraStatusStatisticsBuilder = jiraStatusStatisticsBuilder;
        _settings = options.Value;
    }

    /// <inheritdoc />
    public void RenderHeader() => _consoleRenderer.RenderHeader();

    /// <inheritdoc />
    public void PrintRepositoryCheckCount(int repositoryCount) =>
        _consoleRenderer.PrintRepositoryCheckCount(repositoryCount);

    /// <inheritdoc />
    public void PrintRepositoryBatchProgress(
        int batchNumber,
        int totalBatchCount,
        int processedRepositoryCount,
        int currentBatchRepositoryCount,
        int totalRepositoryCount) =>
        _consoleRenderer.PrintRepositoryBatchProgress(
            batchNumber,
            totalBatchCount,
            processedRepositoryCount,
            currentBatchRepositoryCount,
            totalRepositoryCount);

    /// <inheritdoc />
    public void PrintRunHeading(string? runName, int runIndex, int totalRuns) =>
        _consoleRenderer.PrintRunHeading(runName, runIndex, totalRuns);

    /// <inheritdoc />
    public Task<T> RunBitbucketLoadingWithProgressAsync<T>(
        IReadOnlyList<RepositoryName> repositories,
        Func<BitbucketProgressCallbacks, Task<T>> operation) =>
        _consoleRenderer.RunBitbucketLoadingWithProgressAsync(repositories, operation);

    /// <inheritdoc />
    public void PrintNoRows() => _consoleRenderer.PrintNoRows();

    /// <inheritdoc />
    public void PrintNoRowsMatchedGroup(string runName) => _consoleRenderer.PrintNoRowsMatchedGroup(runName);

    /// <inheritdoc />
    public void PrintNoComponentsMatchedStatusFilter(IReadOnlyList<JiraStatusName> statuses) =>
        _consoleRenderer.PrintNoComponentsMatchedStatusFilter(statuses);

    /// <inheritdoc />
    public void PrintStatusFilterDiagnostics(
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics,
        IReadOnlyList<JiraStatusName> allowedStatuses) =>
        _consoleRenderer.PrintStatusFilterDiagnostics(statusStatistics, allowedStatuses);

    /// <inheritdoc />
    public void RenderResults(IReadOnlyList<ComponentCheckRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var allowedStatuses = _settings.Jira.BuildAllowedStatuses();
        var filteredRows = rows.FilterRowsByAllowedJiraStatuses(allowedStatuses);
        var statusStatistics = _jiraStatusStatisticsBuilder.Build(rows);

        if (filteredRows.Length == 0)
        {
            _consoleRenderer.PrintNoComponentsMatchedStatusFilter(allowedStatuses);
            _consoleRenderer.PrintStatusFilterDiagnostics(statusStatistics, allowedStatuses);
            _excelReportRenderer.RenderReport(filteredRows, allowedStatuses, statusStatistics);
            _pdfReportRenderer.RenderReport(filteredRows, allowedStatuses, statusStatistics);
            return;
        }

        _consoleRenderer.RenderTable(filteredRows);
        _consoleRenderer.RenderSummary(filteredRows);
        _consoleRenderer.RenderUniqueJiraTaskStatusChart(filteredRows);
        _excelReportRenderer.RenderReport(filteredRows, allowedStatuses, statusStatistics);
        _pdfReportRenderer.RenderReport(filteredRows, allowedStatuses, statusStatistics);
    }

    /// <inheritdoc />
    public void RenderTable(IReadOnlyList<ComponentCheckRow> rows) => _consoleRenderer.RenderTable(rows);

    /// <inheritdoc />
    public void RenderSummary(IReadOnlyList<ComponentCheckRow> rows) => _consoleRenderer.RenderSummary(rows);

    /// <inheritdoc />
    public void PrintError(ErrorMessage message) => _consoleRenderer.PrintError(message);

    private readonly IConsoleOutputRenderer _consoleRenderer;
    private readonly IExcelReportRenderer _excelReportRenderer;
    private readonly IPdfReportRenderer _pdfReportRenderer;
    private readonly IJiraStatusStatisticsBuilder _jiraStatusStatisticsBuilder;
    private readonly AppSettings _settings;
}
