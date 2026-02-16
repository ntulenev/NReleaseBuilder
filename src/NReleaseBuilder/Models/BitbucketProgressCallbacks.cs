namespace NReleaseBuilder.Models;

/// <summary>
/// Progress callbacks invoked during per-repository Bitbucket loading.
/// </summary>
public sealed class BitbucketProgressCallbacks
{
    /// <summary>
    /// Called when processing of a repository starts.
    /// </summary>
    public Action<string>? RepositoryStarted { get; init; }

    /// <summary>
    /// Called when the number of commits/tags to inspect becomes known.
    /// </summary>
    public Action<string, int>? CommitTotalDetected { get; init; }

    /// <summary>
    /// Called when a single commit/tag has been processed.
    /// </summary>
    public Action<string>? CommitProcessed { get; init; }

    /// <summary>
    /// Called when processing of a repository completes.
    /// </summary>
    public Action<string>? RepositoryCompleted { get; init; }
}
