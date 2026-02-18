using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Bitbucket.Internal.Models;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

using NuGet.Versioning;

namespace NReleaseBuilder.Bitbucket.Internal;

/// <summary>
/// Default implementation for building Bitbucket repository tag lookups.
/// </summary>
public sealed class BitbucketTagLookupCore : IBitbucketTagLookupCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTagLookupCore"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    /// <param name="bitbucketIntegrationCore">Bitbucket integration abstraction.</param>
    /// <param name="jiraTaskResolver">Jira task resolver.</param>
    public BitbucketTagLookupCore(
        IOptions<AppSettings> options,
        IBitbucketIntegrationCore bitbucketIntegrationCore,
        IJiraTaskResolver jiraTaskResolver)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(bitbucketIntegrationCore);
        ArgumentNullException.ThrowIfNull(jiraTaskResolver);

        _bitbucketOptions = options.Value.Bitbucket;
        _bitbucketIntegrationCore = bitbucketIntegrationCore;
        _jiraTaskResolver = jiraTaskResolver;
    }

    /// <inheritdoc />
    public async Task<RepositoryTagLookup> GetRepositoryTagLookupAsync(
        RepositoryName repository,
        NuGetVersion? minCurrentVersion,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        var (repositoryForBitbucketCalls, tagLoadResult) = await LoadRepositoryTagReferencesWithFallbackAsync(
                repository,
                cancellationToken)
            .ConfigureAwait(false);
        if (TryBuildFailedTagLookup(repositoryForBitbucketCalls, tagLoadResult) is { } failedTagLookup)
        {
            return failedTagLookup;
        }

        var tagsToInspect = SelectTagsToInspect(tagLoadResult.Tags, minCurrentVersion);
        var projectNames = _bitbucketOptions.ResolveProjectNames();
        progress?.CommitTotalDetected?.Invoke(repository.Value, tagsToInspect.Length);

        var enrichedTags = await BuildEnrichedTagsAsync(
            repository,
            repositoryForBitbucketCalls,
            projectNames,
            tagsToInspect,
            progress,
            cancellationToken).ConfigureAwait(false);
        return RepositoryTagLookup.Success(repositoryForBitbucketCalls, enrichedTags);
    }

    private async Task<(RepositoryName RepositoryForBitbucketCalls, RepositoryTagReferenceLoadResult TagLoadResult)> LoadRepositoryTagReferencesWithFallbackAsync(
        RepositoryName repository,
        CancellationToken cancellationToken)
    {
        var repositoryForBitbucketCalls = _bitbucketOptions.ResolveRepositoryName(repository);
        var tagLoadResult = await _bitbucketIntegrationCore.LoadRepositoryTagReferencesAsync(
                repositoryForBitbucketCalls,
                cancellationToken)
            .ConfigureAwait(false);

        if (tagLoadResult.IsRepositoryMissing
            && _bitbucketOptions.UseTruncatedRepositoryNameFallback
            && TryBuildTruncatedRepositoryName(repositoryForBitbucketCalls) is { } truncatedRepository)
        {
            var fallbackLoadResult = await _bitbucketIntegrationCore.LoadRepositoryTagReferencesAsync(
                    truncatedRepository,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!fallbackLoadResult.IsRepositoryMissing)
            {
                repositoryForBitbucketCalls = truncatedRepository;
                tagLoadResult = fallbackLoadResult;
            }
        }

        return (repositoryForBitbucketCalls, tagLoadResult);
    }

    private async Task<List<RepositoryTagInfo>> BuildEnrichedTagsAsync(
        RepositoryName sourceRepository,
        RepositoryName repositoryForBitbucketCalls,
        JiraProjectName[] projectNames,
        RepositoryTagReference[] tagsToInspect,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        var jiraCacheByCommit = new Dictionary<string, JiraTaskResolution>(StringComparer.OrdinalIgnoreCase);
        var enrichedTags = new List<RepositoryTagInfo>(tagsToInspect.Length);

        foreach (var tag in tagsToInspect)
        {
            var jiraResolution = await ResolveJiraTaskResolutionAsync(
                repositoryForBitbucketCalls,
                projectNames,
                tag.CommitHash,
                jiraCacheByCommit,
                cancellationToken).ConfigureAwait(false);

            enrichedTags.Add(new RepositoryTagInfo(
                tag.Name,
                jiraResolution.Tasks,
                jiraResolution.Titles,
                jiraResolution.Statuses,
                jiraResolution.TaskAlertDetails,
                jiraResolution.HasRequiredActions,
                jiraResolution.HasBreakingChanges,
                jiraResolution.HasDependencyIssues));
            progress?.CommitProcessed?.Invoke(sourceRepository.Value);
        }

        return enrichedTags;
    }

    private async Task<JiraTaskResolution> ResolveJiraTaskResolutionAsync(
        RepositoryName repositoryForBitbucketCalls,
        JiraProjectName[] projectNames,
        CommitHash? commitHash,
        Dictionary<string, JiraTaskResolution> jiraCacheByCommit,
        CancellationToken cancellationToken)
    {
        if (commitHash is null)
        {
            return JiraTaskResolution.NotAvailable(JiraTaskReference.NotAvailable);
        }

        if (jiraCacheByCommit.TryGetValue(commitHash.Value.Value, out var jiraResolution))
        {
            return jiraResolution;
        }

        var commitInfo = await _bitbucketIntegrationCore.TryGetCommitMessageAsync(
            repositoryForBitbucketCalls,
            commitHash.Value,
            cancellationToken).ConfigureAwait(false);

        jiraResolution = await _jiraTaskResolver.ResolveFromCommitMessageAsync(
                commitInfo,
                projectNames,
                cancellationToken)
            .ConfigureAwait(false);
        jiraCacheByCommit[commitHash.Value.Value] = jiraResolution;

        return jiraResolution;
    }

    private static RepositoryTagLookup? TryBuildFailedTagLookup(
        RepositoryName repositoryForBitbucketCalls,
        RepositoryTagReferenceLoadResult tagLoadResult)
    {
        if (tagLoadResult.IsRepositoryMissing)
        {
            return RepositoryTagLookup.RepoNotFound(repositoryForBitbucketCalls);
        }

        if (!string.IsNullOrWhiteSpace(tagLoadResult.Error))
        {
            return RepositoryTagLookup.ApiError(repositoryForBitbucketCalls, tagLoadResult.Error);
        }

        return null;
    }

    private static RepositoryTagReference[] SelectTagsToInspect(
        IReadOnlyList<RepositoryTagReference> tags,
        NuGetVersion? minCurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return
        [
            .. tags.Where(tag => minCurrentVersion is null
                || (VersionParser.TryParse(tag.Name, out var parsedVersion) && parsedVersion > minCurrentVersion))
        ];
    }

    private static RepositoryName? TryBuildTruncatedRepositoryName(RepositoryName repository)
    {
        var value = repository.Value;
        var lastDotIndex = value.LastIndexOf('.');

        if (lastDotIndex <= 0 || lastDotIndex == value.Length - 1)
        {
            return null;
        }

        return new RepositoryName(value[..lastDotIndex]);
    }

    private readonly IBitbucketIntegrationCore _bitbucketIntegrationCore;
    private readonly IJiraTaskResolver _jiraTaskResolver;
    private readonly BitbucketOptions _bitbucketOptions;
}
