namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Bitbucket pull request page DTO.
/// </summary>
public sealed class PullRequestPageDto
{
    /// <summary>
    /// Pull request items.
    /// </summary>
    public IReadOnlyList<PullRequestDto>? Values { get; init; }
}
