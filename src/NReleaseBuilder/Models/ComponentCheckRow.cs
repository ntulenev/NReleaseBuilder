namespace NReleaseBuilder.Models;

public readonly record struct ComponentCheckRow(
    int Index,
    string Component,
    string Repository,
    string CurrentVersion,
    CheckStatus Status,
    string DetailsMessage,
    IReadOnlyList<VersionJiraRow> NewerVersions);
