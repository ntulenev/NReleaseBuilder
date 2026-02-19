namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Bitbucket pull request DTO.
/// </summary>
public sealed class PullRequestDto
{
    /// <summary>
    /// Pull request state.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Pull request links payload.
    /// </summary>
    public PullRequestLinksDto? Links { get; init; }
}
