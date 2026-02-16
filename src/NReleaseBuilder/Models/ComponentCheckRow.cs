namespace NReleaseBuilder.Models;

/// <summary>
/// Output row for a component version check.
/// </summary>
/// <param name="Index">Display index in output table.</param>
/// <param name="Component">Component name.</param>
/// <param name="Repository">Repository name.</param>
/// <param name="CurrentVersion">Current component version.</param>
/// <param name="Status">Check status.</param>
/// <param name="DetailsMessage">Additional details message.</param>
/// <param name="NewerVersions">Detected newer versions with Jira details.</param>
public readonly record struct ComponentCheckRow(
    int Index,
    ComponentName Component,
    RepositoryName Repository,
    VersionLabel CurrentVersion,
    CheckStatus Status,
    RowDetails DetailsMessage,
    IReadOnlyList<VersionJiraRow> NewerVersions);
