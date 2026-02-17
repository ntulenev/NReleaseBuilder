using NReleaseBuilder.Models;

namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// PDF report rendering abstraction.
/// </summary>
public interface IPdfReportRenderer
{
    /// <summary>
    /// Renders the PDF report for filtered results.
    /// </summary>
    /// <param name="rows">Filtered rows to render.</param>
    /// <param name="allowedStatuses">Configured allowed Jira statuses.</param>
    /// <param name="statusStatistics">Overall status statistics.</param>
    void RenderReport(
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics);
}
