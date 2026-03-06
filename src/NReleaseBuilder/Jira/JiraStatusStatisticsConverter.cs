using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Jira;

/// <summary>
/// Converts component check rows into aggregate Jira status statistics.
/// </summary>
public sealed class JiraStatusStatisticsConverter : IJiraStatusStatisticsConverter
{
    /// <inheritdoc />
    public Dictionary<JiraStatusName, int> Convert(IReadOnlyList<ComponentCheckRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var statistics = new Dictionary<JiraStatusName, int>();

        foreach (var row in rows)
        {
            foreach (var version in row.NewerVersions)
            {
                var statuses = version.JiraStatus.SplitStatuses();

                foreach (var status in statuses)
                {
                    _ = statistics.TryGetValue(status, out var currentCount);
                    statistics[status] = currentCount + 1;
                }
            }
        }

        return statistics;
    }
}
