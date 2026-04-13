using System.Collections.Concurrent;

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
        var repositoryForBitbucketCalls = repository;
        var tagLoadResult = await _bitbucketIntegrationCore.LoadRepositoryTagReferencesAsync(
                repositoryForBitbucketCalls,
                cancellationToken)
            .ConfigureAwait(false);

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
        var commitInfosByCommit = await LoadCommitInfosAsync(
            repositoryForBitbucketCalls,
            tagsToInspect,
            cancellationToken).ConfigureAwait(false);
        var pullRequestUrlsByCommit = await LoadPullRequestUrlsAsync(
            repositoryForBitbucketCalls,
            tagsToInspect,
            cancellationToken).ConfigureAwait(false);

        await _jiraTaskResolver.PrimeTaskInfoCacheAsync(
            [.. commitInfosByCommit.Values],
            projectNames,
            cancellationToken).ConfigureAwait(false);

        var jiraCacheByCommit = new Dictionary<string, JiraTaskResolution>(StringComparer.OrdinalIgnoreCase);
        var enrichedTags = new List<RepositoryTagInfo>(tagsToInspect.Length);

        foreach (var tag in tagsToInspect)
        {
            var jiraResolution = await ResolveJiraTaskResolutionAsync(
                repositoryForBitbucketCalls,
                commitInfosByCommit,
                projectNames,
                tag.CommitHash,
                jiraCacheByCommit,
                cancellationToken).ConfigureAwait(false);
            var pullRequestUrl = await ResolvePullRequestUrlAsync(
                repositoryForBitbucketCalls,
                pullRequestUrlsByCommit,
                tag.CommitHash,
                cancellationToken).ConfigureAwait(false);

            enrichedTags.Add(new RepositoryTagInfo(
                tag.Name,
                jiraResolution.Tasks,
                jiraResolution.Titles,
                jiraResolution.Statuses,
                jiraResolution.TaskAlertDetails,
                jiraResolution.HasRequiredActions,
                jiraResolution.HasBreakingChanges,
                jiraResolution.HasDependencyIssues,
                pullRequestUrl));
            progress?.CommitProcessed?.Invoke(sourceRepository.Value);
        }

        return enrichedTags;
    }

    private async Task<Dictionary<string, CommitInfo>> LoadCommitInfosAsync(
        RepositoryName repositoryForBitbucketCalls,
        IReadOnlyList<RepositoryTagReference> tagsToInspect,
        CancellationToken cancellationToken)
    {
        var commitHashes = GetDistinctCommitHashes(tagsToInspect);
        return await LoadByCommitHashAsync(
            commitHashes,
            (commitHash, token) => _bitbucketIntegrationCore.TryGetCommitMessageAsync(
                repositoryForBitbucketCalls,
                commitHash,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, Uri?>> LoadPullRequestUrlsAsync(
        RepositoryName repositoryForBitbucketCalls,
        IReadOnlyList<RepositoryTagReference> tagsToInspect,
        CancellationToken cancellationToken)
    {
        var commitHashes = GetDistinctCommitHashes(tagsToInspect);
        return await LoadByCommitHashAsync(
            commitHashes,
            (commitHash, token) => _bitbucketIntegrationCore.TryGetPullRequestUrlByCommitAsync(
                repositoryForBitbucketCalls,
                commitHash,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, TValue>> LoadByCommitHashAsync<TValue>(
        CommitHash[] commitHashes,
        Func<CommitHash, CancellationToken, Task<TValue>> loader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commitHashes);
        ArgumentNullException.ThrowIfNull(loader);

        if (commitHashes.Length == 0)
        {
            return new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        }

        var resultsByCommit = new ConcurrentDictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        using var semaphore = new SemaphoreSlim(
            _bitbucketOptions.MaxParallelRequests,
            _bitbucketOptions.MaxParallelRequests);

        var loadTasks = commitHashes.Select(async commitHash =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                resultsByCommit[commitHash.Value] = await loader(
                    commitHash,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = semaphore.Release();
            }
        });

        await Task.WhenAll(loadTasks).ConfigureAwait(false);
        return new Dictionary<string, TValue>(resultsByCommit, StringComparer.OrdinalIgnoreCase);
    }

    private static CommitHash[] GetDistinctCommitHashes(IReadOnlyList<RepositoryTagReference> tagsToInspect)
    {
        ArgumentNullException.ThrowIfNull(tagsToInspect);

        return
        [
            .. tagsToInspect
                .Select(static tag => tag.CommitHash)
                .Where(static commitHash => commitHash is not null)
                .Select(static commitHash => commitHash!.Value)
                .Distinct()
        ];
    }

    private async Task<JiraTaskResolution> ResolveJiraTaskResolutionAsync(
        RepositoryName repositoryForBitbucketCalls,
        Dictionary<string, CommitInfo> commitInfosByCommit,
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

        if (!commitInfosByCommit.TryGetValue(commitHash.Value.Value, out var commitInfo))
        {
            commitInfo = await _bitbucketIntegrationCore.TryGetCommitMessageAsync(
                repositoryForBitbucketCalls,
                commitHash.Value,
                cancellationToken).ConfigureAwait(false);
        }

        jiraResolution = await _jiraTaskResolver.ResolveFromCommitMessageAsync(
                commitInfo,
                projectNames,
                cancellationToken)
            .ConfigureAwait(false);
        jiraCacheByCommit[commitHash.Value.Value] = jiraResolution;

        return jiraResolution;
    }

    private async Task<Uri?> ResolvePullRequestUrlAsync(
        RepositoryName repositoryForBitbucketCalls,
        Dictionary<string, Uri?> pullRequestUrlsByCommit,
        CommitHash? commitHash,
        CancellationToken cancellationToken)
    {
        if (commitHash is null)
        {
            return null;
        }

        if (pullRequestUrlsByCommit.TryGetValue(commitHash.Value.Value, out var pullRequestUrl))
        {
            return pullRequestUrl;
        }

        return await _bitbucketIntegrationCore.TryGetPullRequestUrlByCommitAsync(
            repositoryForBitbucketCalls,
            commitHash.Value,
            cancellationToken).ConfigureAwait(false);
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

    private readonly IBitbucketIntegrationCore _bitbucketIntegrationCore;
    private readonly IJiraTaskResolver _jiraTaskResolver;
    private readonly BitbucketOptions _bitbucketOptions;
}
