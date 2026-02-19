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

    /// <summary>
    /// URL to the next page when pagination is available.
    /// </summary>
    public string? Next { get; init; }
}
