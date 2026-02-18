namespace NReleaseBuilder.Models.Bitbucket;

/// <summary>
/// Commit domain model with message payload.
/// </summary>
public sealed class CommitInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommitInfo"/> class.
    /// </summary>
    /// <param name="message">Commit message text.</param>
    public CommitInfo(string? message)
    {
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
    }

    /// <summary>
    /// Commit message text.
    /// </summary>
    public string? Message { get; }
}
