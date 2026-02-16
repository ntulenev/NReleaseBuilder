namespace NReleaseBuilder.Models;

/// <summary>
/// Repository tag reference with optional commit hash.
/// </summary>
/// <param name="Name">Tag name.</param>
/// <param name="CommitHash">Commit hash associated with tag.</param>
public readonly record struct RepositoryTagReference(string Name, string? CommitHash);
