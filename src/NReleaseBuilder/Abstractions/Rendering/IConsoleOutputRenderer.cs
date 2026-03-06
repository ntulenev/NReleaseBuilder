

using NReleaseBuilder.Models;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// Console-only rendering abstraction.
/// </summary>
public interface IConsoleOutputRenderer
{
    /// <summary>
    /// Renders the initial run header.
    /// </summary>
    void RenderHeader();

    /// <summary>
    /// Prints the number of repositories to be checked.
    /// </summary>
    /// <param name="repositoryCount">Repository count.</param>
    void PrintRepositoryCheckCount(int repositoryCount);

    /// <summary>
    /// Prints progress information for the current repository loading batch.
    /// </summary>
    /// <param name="batchNumber">Current batch number (1-based).</param>
    /// <param name="totalBatchCount">Total batch count.</param>
    /// <param name="processedRepositoryCount">Already processed repository count.</param>
    /// <param name="currentBatchRepositoryCount">Repository count in current batch.</param>
    /// <param name="totalRepositoryCount">Total repository count.</param>
    void PrintRepositoryBatchProgress(
        int batchNumber,
        int totalBatchCount,
        int processedRepositoryCount,
        int currentBatchRepositoryCount,
        int totalRepositoryCount);

    /// <summary>
    /// Prints a heading for the current report run group.
    /// </summary>
    /// <param name="runName">Run group name.</param>
    /// <param name="runIndex">Zero-based run index.</param>
    /// <param name="totalRuns">Total run count.</param>
    void PrintRunHeading(string? runName, int runIndex, int totalRuns);

    /// <summary>
    /// Runs an asynchronous operation with Bitbucket loading progress UI.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="repositories">Repositories participating in the operation.</param>
    /// <param name="operation">Operation to execute.</param>
    /// <returns>Operation result.</returns>
    Task<T> RunBitbucketLoadingWithProgressAsync<T>(
        IReadOnlyList<RepositoryName> repositories,
        Func<BitbucketProgressCallbacks, Task<T>> operation);

    /// <summary>
    /// Prints a message when CSV has no rows.
    /// </summary>
    void PrintNoRows();

    /// <summary>
    /// Prints a message when CSV has no rows for a specific run group.
    /// </summary>
    /// <param name="runName">Run group name.</param>
    void PrintNoRowsMatchedGroup(string runName);

    /// <summary>
    /// Prints a message when no component matches the Jira status filter.
    /// </summary>
    /// <param name="statuses">Configured statuses.</param>
    void PrintNoComponentsMatchedStatusFilter(IReadOnlyList<JiraStatusName> statuses);

    /// <summary>
    /// Prints status diagnostics for disallowed Jira statuses.
    /// </summary>
    /// <param name="statusStatistics">Status counters.</param>
    /// <param name="allowedStatuses">Allowed statuses.</param>
    void PrintStatusFilterDiagnostics(
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics,
        IReadOnlyList<JiraStatusName> allowedStatuses);

    /// <summary>
    /// Renders component status table.
    /// </summary>
    /// <param name="rows">Rows to render.</param>
    void RenderTable(IReadOnlyList<ComponentCheckRow> rows);

    /// <summary>
    /// Renders status summary.
    /// </summary>
    /// <param name="rows">Rows to summarize.</param>
    void RenderSummary(IReadOnlyList<ComponentCheckRow> rows);

    /// <summary>
    /// Renders unique Jira task distribution chart by status.
    /// </summary>
    /// <param name="rows">Rows to analyze.</param>
    void RenderUniqueJiraTaskStatusChart(IReadOnlyList<ComponentCheckRow> rows);

    /// <summary>
    /// Prints an error message.
    /// </summary>
    /// <param name="message">Error details.</param>
    void PrintError(ErrorMessage message);
}
