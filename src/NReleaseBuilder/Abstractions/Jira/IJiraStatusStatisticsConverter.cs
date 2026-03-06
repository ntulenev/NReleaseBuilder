

using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Abstractions.Jira;

/// <summary>
/// Converts component check rows to Jira status statistics.
/// </summary>
public interface IJiraStatusStatisticsConverter
{
    /// <summary>
    /// Converts newer versions in component check rows to Jira status counters.
    /// </summary>
    /// <param name="rows">Component check rows.</param>
    /// <returns>Status counters by Jira status name.</returns>
    Dictionary<JiraStatusName, int> Convert(IReadOnlyList<ComponentCheckRow> rows);
}
