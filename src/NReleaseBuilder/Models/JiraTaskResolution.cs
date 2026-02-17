namespace NReleaseBuilder.Models;

/// <summary>
/// Resolved Jira details for one or more Jira tasks.
/// </summary>
public readonly record struct JiraTaskResolution
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTaskResolution"/> struct from Jira reference collections.
    /// </summary>
    /// <param name="statuses">Resolved Jira statuses.</param>
    /// <param name="tasks">Resolved Jira task keys.</param>
    /// <param name="titles">Resolved Jira issue titles.</param>
    /// <param name="taskAlertDetails">Per-task alert details.</param>
    /// <param name="hasRequiredActions">Whether any task has required actions.</param>
    /// <param name="hasBreakingChanges">Whether any task has breaking changes.</param>
    /// <param name="hasDependencyIssues">Whether dependency issues were detected.</param>
    public JiraTaskResolution(
        IReadOnlyList<JiraStatusReference> statuses,
        IReadOnlyList<JiraTaskReference> tasks,
        IReadOnlyList<JiraTitleReference> titles,
        IReadOnlyList<JiraTaskAlertDetails> taskAlertDetails,
        bool hasRequiredActions,
        bool hasBreakingChanges,
        bool hasDependencyIssues)
        : this(
            CombineStatuses(statuses),
            CombineTasks(tasks),
            CombineTitles(titles),
            taskAlertDetails,
            hasRequiredActions,
            hasBreakingChanges,
            hasDependencyIssues)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTaskResolution"/> struct.
    /// </summary>
    /// <param name="statuses">Resolved Jira statuses reference.</param>
    /// <param name="tasks">Resolved Jira task keys reference.</param>
    /// <param name="titles">Resolved Jira issue titles reference.</param>
    /// <param name="taskAlertDetails">Per-task alert details.</param>
    /// <param name="hasRequiredActions">Whether any task has required actions.</param>
    /// <param name="hasBreakingChanges">Whether any task has breaking changes.</param>
    /// <param name="hasDependencyIssues">Whether dependency issues were detected.</param>
    public JiraTaskResolution(
        JiraStatusReference statuses,
        JiraTaskReference tasks,
        JiraTitleReference titles,
        IReadOnlyList<JiraTaskAlertDetails> taskAlertDetails,
        bool hasRequiredActions,
        bool hasBreakingChanges,
        bool hasDependencyIssues)
    {
        ArgumentNullException.ThrowIfNull(taskAlertDetails);

        Statuses = statuses;
        Tasks = tasks;
        Titles = titles;
        TaskAlertDetails = taskAlertDetails;
        HasRequiredActions = hasRequiredActions;
        HasBreakingChanges = hasBreakingChanges;
        HasDependencyIssues = hasDependencyIssues;
    }

    /// <summary>
    /// Creates a fallback resolution when Jira metadata is not available.
    /// </summary>
    /// <param name="tasks">Original Jira task value.</param>
    /// <returns>Fallback Jira resolution.</returns>
    public static JiraTaskResolution NotAvailable(JiraTaskReference tasks)
    {
        return new JiraTaskResolution(
            new JiraStatusReference("N/A"),
            tasks,
            new JiraTitleReference("N/A"),
            [],
            false,
            false,
            false);
    }

    /// <summary>
    /// Resolved Jira statuses.
    /// </summary>
    public JiraStatusReference Statuses { get; }

    /// <summary>
    /// Resolved Jira task keys.
    /// </summary>
    public JiraTaskReference Tasks { get; }

    /// <summary>
    /// Resolved Jira issue titles.
    /// </summary>
    public JiraTitleReference Titles { get; }

    /// <summary>
    /// Per-task alert details.
    /// </summary>
    public IReadOnlyList<JiraTaskAlertDetails> TaskAlertDetails { get; }

    /// <summary>
    /// Whether any task has required actions.
    /// </summary>
    public bool HasRequiredActions { get; }

    /// <summary>
    /// Whether any task has breaking changes.
    /// </summary>
    public bool HasBreakingChanges { get; }

    /// <summary>
    /// Whether dependency issues were detected.
    /// </summary>
    public bool HasDependencyIssues { get; }

    private static JiraStatusReference CombineStatuses(IReadOnlyList<JiraStatusReference> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new JiraStatusReference(string.Join(", ", values.Select(static value => value.Value)));
    }

    private static JiraTaskReference CombineTasks(IReadOnlyList<JiraTaskReference> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new JiraTaskReference(string.Join(", ", values.Select(static value => value.Value)));
    }

    private static JiraTitleReference CombineTitles(IReadOnlyList<JiraTitleReference> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new JiraTitleReference(string.Join(", ", values.Select(static value => value.Value)));
    }
}
