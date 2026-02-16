namespace NReleaseBuilder.Transport;

/// <summary>
/// Bitbucket commit DTO.
/// </summary>
public sealed class CommitDto
{
    /// <summary>
    /// Commit message.
    /// </summary>
    public string? Message { get; init; }
}
