namespace NReleaseBuilder.Models;

public sealed class BitbucketProgressCallbacks
{
    public Action<string>? RepositoryStarted { get; init; }
    public Action<string, int>? CommitTotalDetected { get; init; }
    public Action<string>? CommitProcessed { get; init; }
    public Action<string>? RepositoryCompleted { get; init; }
}
