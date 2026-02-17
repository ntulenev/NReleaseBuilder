namespace NReleaseBuilder.Models;

/// <summary>
/// Resolved Jira details for one or more Jira tasks.
/// </summary>
public readonly record struct JiraTaskResolution(
    string Statuses,
    string Tasks,
    string Titles,
    IReadOnlyList<JiraTaskAlertDetails> TaskAlertDetails,
    bool HasRequiredActions,
    bool HasBreakingChanges,
    bool HasDependencyIssues)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTaskResolution"/> struct from task collections.
    /// </summary>
    /// <param name="statuses">Resolved Jira statuses.</param>
    /// <param name="tasks">Resolved Jira task keys.</param>
    /// <param name="titles">Resolved Jira issue titles.</param>
    /// <param name="taskAlertDetails">Per-task alert details.</param>
    /// <param name="hasRequiredActions">Whether any task has required actions.</param>
    /// <param name="hasBreakingChanges">Whether any task has breaking changes.</param>
    /// <param name="hasDependencyIssues">Whether dependency issues were detected.</param>
    public JiraTaskResolution(
        IReadOnlyList<string> statuses,
        IReadOnlyList<string> tasks,
        IReadOnlyList<string> titles,
        IReadOnlyList<JiraTaskAlertDetails> taskAlertDetails,
        bool hasRequiredActions,
        bool hasBreakingChanges,
        bool hasDependencyIssues)
        : this(
            JoinValues(statuses),
            JoinValues(tasks),
            JoinValues(titles),
            taskAlertDetails,
            hasRequiredActions,
            hasBreakingChanges,
            hasDependencyIssues)
    {
    }

    /// <summary>
    /// Creates a fallback resolution when Jira metadata is not available.
    /// </summary>
    /// <param name="tasks">Original Jira task value.</param>
    /// <returns>Fallback Jira resolution.</returns>
    public static JiraTaskResolution NotAvailable(string tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        return new JiraTaskResolution("N/A", tasks, "N/A", [], false, false, false);
    }

    private static string JoinValues(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return string.Join(", ", values);
    }
}
