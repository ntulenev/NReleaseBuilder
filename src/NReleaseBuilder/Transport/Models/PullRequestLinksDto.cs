namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Pull request links DTO.
/// </summary>
public sealed class PullRequestLinksDto
{
    /// <summary>
    /// HTML link payload.
    /// </summary>
    public PullRequestLinkDto? Html { get; init; }
}
