using NReleaseBuilder.Models;

namespace NReleaseBuilder.Abstractions;

/// <summary>
/// Console rendering abstraction for application output.
/// </summary>
public interface IConsoleRenderer
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
    /// Renders final filtered results with diagnostics when nothing matches.
    /// </summary>
    /// <param name="rows">Rows to render.</param>
    void RenderResults(IReadOnlyList<ComponentCheckRow> rows);

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
    /// Renders plain text ready for Slack.
    /// </summary>
    /// <param name="rows">Rows to include.</param>
    /// <param name="allowedStatuses">Configured Jira filter.</param>
    void RenderSlackCopyText(
        IReadOnlyList<ComponentCheckRow> rows,
        IReadOnlyList<JiraStatusName> allowedStatuses);

    /// <summary>
    /// Prints an error message.
    /// </summary>
    /// <param name="message">Error details.</param>
    void PrintError(ErrorMessage message);
}
