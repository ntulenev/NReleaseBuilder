namespace NReleaseBuilder.Models;

/// <summary>
/// Output row for a component version check.
/// </summary>
public readonly record struct ComponentCheckRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentCheckRow"/> struct.
    /// </summary>
    /// <param name="index">Display index in output table.</param>
    /// <param name="component">Component name.</param>
    /// <param name="repository">Repository name.</param>
    /// <param name="currentVersion">Current component version.</param>
    /// <param name="status">Check status.</param>
    /// <param name="detailsMessage">Additional details message.</param>
    /// <param name="newerVersions">Detected newer versions with Jira details.</param>
    public ComponentCheckRow(
        ComponentCheckIndex index,
        ComponentName component,
        RepositoryName repository,
        VersionLabel currentVersion,
        CheckStatus status,
        RowDetails detailsMessage,
        IReadOnlyList<VersionJiraRow> newerVersions)
    {
        ArgumentNullException.ThrowIfNull(newerVersions);

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown check status value.");
        }

        Index = index;
        Component = component;
        Repository = repository;
        CurrentVersion = currentVersion;
        Status = status;
        DetailsMessage = detailsMessage;
        NewerVersions = newerVersions;
    }

    /// <summary>
    /// Display index in output table.
    /// </summary>
    public ComponentCheckIndex Index { get; }

    /// <summary>
    /// Component name.
    /// </summary>
    public ComponentName Component { get; }

    /// <summary>
    /// Repository name.
    /// </summary>
    public RepositoryName Repository { get; }

    /// <summary>
    /// Current component version.
    /// </summary>
    public VersionLabel CurrentVersion { get; }

    /// <summary>
    /// Check status.
    /// </summary>
    public CheckStatus Status { get; }

    /// <summary>
    /// Additional details message.
    /// </summary>
    public RowDetails DetailsMessage { get; }

    /// <summary>
    /// Detected newer versions with Jira details.
    /// </summary>
    public IReadOnlyList<VersionJiraRow> NewerVersions { get; }

    /// <summary>
    /// Checks whether this row matches the provided Jira status filter.
    /// </summary>
    /// <param name="allowedStatuses">Allowed Jira statuses.</param>
    /// <returns>
    /// <see langword="true"/> when row has at least one Jira status and all of them are allowed;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool MatchesStatusFilter(HashSet<JiraStatusName> allowedStatuses)
    {
        ArgumentNullException.ThrowIfNull(allowedStatuses);

        var hasAnyTaskStatus = false;

        foreach (var newerVersion in NewerVersions)
        {
            var statuses = newerVersion.JiraStatus.SplitStatuses();

            foreach (var status in statuses)
            {
                hasAnyTaskStatus = true;

                if (!allowedStatuses.Contains(status))
                {
                    return false;
                }
            }
        }

        return hasAnyTaskStatus;
    }
}
