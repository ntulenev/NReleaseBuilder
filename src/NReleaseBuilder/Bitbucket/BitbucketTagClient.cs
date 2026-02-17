using System.Net;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Bitbucket.Internal;
using NReleaseBuilder.Bitbucket.Internal.Models;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Helpers;
using NReleaseBuilder.Models;
using NReleaseBuilder.Transport;
using NReleaseBuilder.Transport.Models;

using Microsoft.Extensions.Options;

using NuGet.Versioning;

namespace NReleaseBuilder.Bitbucket;

/// <summary>
/// Bitbucket data loader for repository tags and Jira-enriched tag metadata.
/// </summary>
public sealed class BitbucketTagClient : IBitbucketTagClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTagClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="options">Application settings options.</param>
    /// <param name="jiraTaskResolver">Jira task resolver.</param>
    /// <param name="httpRetryExecutor">Shared HTTP retry executor.</param>
    /// <param name="responseSerializer">HTTP response serializer.</param>
    public BitbucketTagClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AppSettings> options,
        IJiraTaskResolver jiraTaskResolver,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jiraTaskResolver);
        ArgumentNullException.ThrowIfNull(httpRetryExecutor);
        ArgumentNullException.ThrowIfNull(responseSerializer);

        var appSettings = options.Value;

        _httpClientFactory = httpClientFactory;
        _bitbucketOptions = appSettings.Bitbucket;
        _jiraTaskResolver = jiraTaskResolver;
        _httpRetryExecutor = httpRetryExecutor;
        _responseSerializer = responseSerializer;
    }

    /// <inheritdoc />
    public async Task<Dictionary<RepositoryName, RepositoryTagLookup>> FetchRepositoryTagLookupsAsync(
        IReadOnlyList<RepositoryName> repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minCurrentVersionsByRepository,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(minCurrentVersionsByRepository);

        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.BITBUCKET);
        using var semaphore = new SemaphoreSlim(
            _bitbucketOptions.MaxParallelRequests,
            _bitbucketOptions.MaxParallelRequests);

        var tasks = repositories.Select(async repository =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                progress?.RepositoryStarted?.Invoke(repository.Value);

                _ = minCurrentVersionsByRepository.TryGetValue(repository, out var minCurrentVersion);
                var lookup = await GetRepositoryTagLookupAsync(
                        httpClient,
                        _httpRetryExecutor,
                        _responseSerializer,
                        _jiraTaskResolver,
                        _bitbucketOptions,
                        repository,
                        minCurrentVersion,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

                return (Repository: repository, Lookup: lookup);
            }
            finally
            {
                progress?.RepositoryCompleted?.Invoke(repository.Value);
                _ = semaphore.Release();
            }
        }).ToArray();

        var pairs = await Task.WhenAll(tasks).ConfigureAwait(false);
        return pairs.ToDictionary(x => x.Repository, x => x.Lookup);
    }

    private static async Task<RepositoryTagLookup> GetRepositoryTagLookupAsync(
        HttpClient httpClient,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer,
        IJiraTaskResolver jiraTaskResolver,
        BitbucketOptions options,
        RepositoryName repository,
        NuGetVersion? minCurrentVersion,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpRetryExecutor);
        ArgumentNullException.ThrowIfNull(responseSerializer);
        ArgumentNullException.ThrowIfNull(jiraTaskResolver);

        var (repositoryForBitbucketCalls, tagLoadResult) = await LoadRepositoryTagReferencesWithFallbackAsync(
                httpClient,
                httpRetryExecutor,
                responseSerializer,
                options,
                repository,
                cancellationToken)
            .ConfigureAwait(false);
        if (TryBuildFailedTagLookup(repositoryForBitbucketCalls, tagLoadResult) is { } failedTagLookup)
        {
            return failedTagLookup;
        }

        var tagsToInspect = SelectTagsToInspect(tagLoadResult.Tags, minCurrentVersion);
        var projectNames = options.ResolveProjectNames();
        progress?.CommitTotalDetected?.Invoke(repository.Value, tagsToInspect.Length);

        var enrichedTags = await BuildEnrichedTagsAsync(
            httpClient,
            httpRetryExecutor,
            responseSerializer,
            jiraTaskResolver,
            options,
            repository,
            repositoryForBitbucketCalls,
            projectNames,
            tagsToInspect,
            progress,
            cancellationToken).ConfigureAwait(false);
        return RepositoryTagLookup.Success(repositoryForBitbucketCalls, enrichedTags);
    }

    private static async Task<(RepositoryName RepositoryForBitbucketCalls, RepositoryTagReferenceLoadResult TagLoadResult)> LoadRepositoryTagReferencesWithFallbackAsync(
        HttpClient httpClient,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer,
        BitbucketOptions options,
        RepositoryName repository,
        CancellationToken cancellationToken)
    {
        var repositoryForBitbucketCalls = options.ResolveRepositoryName(repository);
        var tagLoadResult = await LoadRepositoryTagReferencesAsync(
                httpClient,
                httpRetryExecutor,
                responseSerializer,
                options,
                repositoryForBitbucketCalls,
                cancellationToken)
            .ConfigureAwait(false);

        if (tagLoadResult.IsRepositoryMissing
            && options.UseTruncatedRepositoryNameFallback
            && TryBuildTruncatedRepositoryName(repositoryForBitbucketCalls) is { } truncatedRepository)
        {
            var fallbackLoadResult = await LoadRepositoryTagReferencesAsync(
                    httpClient,
                    httpRetryExecutor,
                    responseSerializer,
                    options,
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

    private static async Task<List<RepositoryTagInfo>> BuildEnrichedTagsAsync(
        HttpClient httpClient,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer,
        IJiraTaskResolver jiraTaskResolver,
        BitbucketOptions options,
        RepositoryName sourceRepository,
        RepositoryName repositoryForBitbucketCalls,
        string[] projectNames,
        RepositoryTagReference[] tagsToInspect,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        var jiraCacheByCommit = new Dictionary<string, JiraTaskResolution>(StringComparer.OrdinalIgnoreCase);
        var enrichedTags = new List<RepositoryTagInfo>(tagsToInspect.Length);

        foreach (var tag in tagsToInspect)
        {
            var jiraResolution = await ResolveJiraTaskResolutionAsync(
                httpClient,
                httpRetryExecutor,
                responseSerializer,
                jiraTaskResolver,
                options,
                repositoryForBitbucketCalls,
                projectNames,
                tag.CommitHash,
                jiraCacheByCommit,
                cancellationToken).ConfigureAwait(false);

            enrichedTags.Add(new RepositoryTagInfo(
                new VersionLabel(tag.Name),
                new JiraTaskReference(jiraResolution.Tasks),
                new JiraTitleReference(jiraResolution.Titles),
                new JiraStatusReference(jiraResolution.Statuses),
                jiraResolution.TaskAlertDetails,
                jiraResolution.HasRequiredActions,
                jiraResolution.HasBreakingChanges,
                jiraResolution.HasDependencyIssues));
            progress?.CommitProcessed?.Invoke(sourceRepository.Value);
        }

        return enrichedTags;
    }

    private static async Task<JiraTaskResolution> ResolveJiraTaskResolutionAsync(
        HttpClient httpClient,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer,
        IJiraTaskResolver jiraTaskResolver,
        BitbucketOptions options,
        RepositoryName repositoryForBitbucketCalls,
        string[] projectNames,
        string? commitHash,
        Dictionary<string, JiraTaskResolution> jiraCacheByCommit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commitHash))
        {
            return JiraTaskResolution.NotAvailable("N/A");
        }

        if (jiraCacheByCommit.TryGetValue(commitHash, out var jiraResolution))
        {
            return jiraResolution;
        }

        var message = await TryGetCommitMessageAsync(
            httpClient,
            httpRetryExecutor,
            responseSerializer,
            options,
            repositoryForBitbucketCalls,
            commitHash,
            cancellationToken).ConfigureAwait(false);

        jiraResolution = await jiraTaskResolver.ResolveFromCommitMessageAsync(
                message,
                projectNames,
                cancellationToken)
            .ConfigureAwait(false);
        jiraCacheByCommit[commitHash] = jiraResolution;

        return jiraResolution;
    }

    private static async Task<RepositoryTagReferenceLoadResult> LoadRepositoryTagReferencesAsync(
        HttpClient httpClient,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer,
        BitbucketOptions options,
        RepositoryName repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpRetryExecutor);
        ArgumentNullException.ThrowIfNull(responseSerializer);

        var tags = new List<RepositoryTagReference>();

        Uri? next = new(
            $"repositories/{Uri.EscapeDataString(options.Workspace)}/{Uri.EscapeDataString(repository.Value)}/refs/tags?pagelen={options.PageLen}",
            UriKind.Relative);

        while (next is not null)
        {
            using var response = await httpRetryExecutor.GetAsync(
                httpClient,
                next,
                options.RetryCount,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return RepositoryTagReferenceLoadResult.RepoNotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var details = string.IsNullOrWhiteSpace(body)
                    ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
                    : $"{(int)response.StatusCode} {response.ReasonPhrase}: {StringHelpers.TrimText(body, 180)}";

                return RepositoryTagReferenceLoadResult.ApiError(details);
            }

            var page = await ReadTagPageAsync(
                response,
                responseSerializer,
                cancellationToken).ConfigureAwait(false);
            tags.AddRange(page.Values);
            next = page.Next;
        }

        var distinctTags = tags
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        return RepositoryTagReferenceLoadResult.Success(distinctTags);
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

    private static async Task<string?> TryGetCommitMessageAsync(
        HttpClient httpClient,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer,
        BitbucketOptions options,
        RepositoryName repository,
        string commitHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpRetryExecutor);
        ArgumentNullException.ThrowIfNull(responseSerializer);

        var commitUrl = new Uri(
            $"repositories/{Uri.EscapeDataString(options.Workspace)}/{Uri.EscapeDataString(repository.Value)}/commit/{Uri.EscapeDataString(commitHash)}",
            UriKind.Relative);

        using var response = await httpRetryExecutor.GetAsync(
            httpClient,
            commitUrl,
            options.RetryCount,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var commit = await ReadCommitInfoAsync(
            response,
            responseSerializer,
            cancellationToken).ConfigureAwait(false);
        return commit?.Message;
    }

    private static async Task<RepositoryTagPage> ReadTagPageAsync(
        HttpResponseMessage response,
        IResponseSerializer responseSerializer,
        CancellationToken cancellationToken)
    {
        var pageDto = await responseSerializer.SerializeAsync<TagPageDto>(
            response,
            cancellationToken).ConfigureAwait(false);

        return pageDto is null ? RepositoryTagPage.Empty : pageDto.ToDomain();
    }

    private static async Task<CommitInfo?> ReadCommitInfoAsync(
        HttpResponseMessage response,
        IResponseSerializer responseSerializer,
        CancellationToken cancellationToken)
    {
        var commitDto = await responseSerializer.SerializeAsync<CommitDto>(
            response,
            cancellationToken).ConfigureAwait(false);

        return commitDto?.ToDomain();
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJiraTaskResolver _jiraTaskResolver;
    private readonly IHttpRetryExecutor _httpRetryExecutor;
    private readonly IResponseSerializer _responseSerializer;
    private readonly BitbucketOptions _bitbucketOptions;
}
