
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// Excel report rendering abstraction.
/// </summary>
public interface IExcelReportRenderer
{
    /// <summary>
    /// Renders the Excel report for filtered results.
    /// </summary>
    /// <param name="rows">Filtered rows to render.</param>
    /// <param name="allowedStatuses">Configured allowed Jira statuses.</param>
    /// <param name="statusStatistics">Overall status statistics.</param>
    void RenderReport(
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics);
}
