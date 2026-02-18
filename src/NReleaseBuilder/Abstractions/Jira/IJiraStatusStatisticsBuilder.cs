

using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Abstractions.Jira;

/// <summary>
/// Builds Jira status statistics from component check rows.
/// </summary>
public interface IJiraStatusStatisticsBuilder
{
    /// <summary>
    /// Builds Jira status counters from newer versions in component check rows.
    /// </summary>
    /// <param name="rows">Component check rows.</param>
    /// <returns>Status counters by Jira status name.</returns>
    Dictionary<JiraStatusName, int> Build(IReadOnlyList<ComponentCheckRow> rows);
}
