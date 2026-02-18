

using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

using QuestPDF.Fluent;

namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// Composes PDF page content section for report output.
/// </summary>
public interface IPdfContentComposer
{
    /// <summary>
    /// Composes the report content body.
    /// </summary>
    /// <param name="column">QuestPDF content column descriptor.</param>
    /// <param name="rows">Filtered component rows.</param>
    /// <param name="allowedStatuses">Configured allowed Jira statuses.</param>
    /// <param name="statusStatistics">Overall Jira status statistics.</param>
    void ComposeContent(
        ColumnDescriptor column,
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics);
}
