
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Models.Rendering;

namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// Composes Excel workbook content for report output.
/// </summary>
public interface IExcelContentComposer
{
    /// <summary>
    /// Composes the report workbook.
    /// </summary>
    /// <param name="rows">Filtered component rows.</param>
    /// <param name="allowedStatuses">Configured allowed Jira statuses.</param>
    /// <param name="statusStatistics">Overall Jira status statistics.</param>
    /// <returns>The workbook sheets and layout metadata.</returns>
    ExcelWorkbookData ComposeWorkbook(
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics);
}
