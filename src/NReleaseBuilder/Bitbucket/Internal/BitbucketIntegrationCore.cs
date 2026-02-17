using System.Net;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Transport;
using NReleaseBuilder.Bitbucket.Internal.Models;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using NReleaseBuilder.Transport;
using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Bitbucket.Internal;

/// <summary>
/// Bitbucket integration service that loads tag and commit data from Bitbucket API.
/// </summary>
public sealed class BitbucketIntegrationCore : IBitbucketIntegrationCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketIntegrationCore"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="options">Application settings options.</param>
    /// <param name="httpRetryExecutor">Shared HTTP retry executor.</param>
    /// <param name="responseSerializer">HTTP response serializer.</param>
    public BitbucketIntegrationCore(
        IHttpClientFactory httpClientFactory,
        IOptions<AppSettings> options,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpRetryExecutor);
        ArgumentNullException.ThrowIfNull(responseSerializer);

        _httpClientFactory = httpClientFactory;
        _httpRetryExecutor = httpRetryExecutor;
        _responseSerializer = responseSerializer;
        _bitbucketOptions = options.Value.Bitbucket;
    }

    /// <inheritdoc />
    public async Task<RepositoryTagReferenceLoadResult> LoadRepositoryTagReferencesAsync(
        RepositoryName repository,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.BITBUCKET);
        var tags = new List<RepositoryTagReference>();

        Uri? next = new(
            $"repositories/{Uri.EscapeDataString(_bitbucketOptions.Workspace)}/{Uri.EscapeDataString(repository.Value)}/refs/tags?pagelen={_bitbucketOptions.PageLen}",
            UriKind.Relative);

        while (next is not null)
        {
            using var response = await _httpRetryExecutor.GetAsync(
                httpClient,
                next,
                _bitbucketOptions.RetryCount,
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
                    : $"{(int)response.StatusCode} {response.ReasonPhrase}: {TrimText(body, 180)}";

                return RepositoryTagReferenceLoadResult.ApiError(details);
            }

            var page = await ReadTagPageAsync(response, cancellationToken).ConfigureAwait(false);
            tags.AddRange(page.Values);
            next = page.Next;
        }

        var distinctTags = tags
            .GroupBy(x => x.Name.Value, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        return RepositoryTagReferenceLoadResult.Success(distinctTags);
    }

    /// <inheritdoc />
    public async Task<CommitInfo> TryGetCommitMessageAsync(
        RepositoryName repository,
        CommitHash commitHash,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.BITBUCKET);
        var commitUrl = new Uri(
            $"repositories/{Uri.EscapeDataString(_bitbucketOptions.Workspace)}/{Uri.EscapeDataString(repository.Value)}/commit/{Uri.EscapeDataString(commitHash.Value)}",
            UriKind.Relative);

        using var response = await _httpRetryExecutor.GetAsync(
            httpClient,
            commitUrl,
            _bitbucketOptions.RetryCount,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new CommitInfo(null);
        }

        var commit = await ReadCommitInfoAsync(response, cancellationToken).ConfigureAwait(false);
        return commit ?? new CommitInfo(null);
    }

    private async Task<RepositoryTagPage> ReadTagPageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var pageDto = await _responseSerializer.SerializeAsync<TagPageDto>(
            response,
            cancellationToken).ConfigureAwait(false);

        return pageDto is null ? RepositoryTagPage.Empty : pageDto.ToDomain();
    }

    private async Task<CommitInfo?> ReadCommitInfoAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var commitDto = await _responseSerializer.SerializeAsync<CommitDto>(
            response,
            cancellationToken).ConfigureAwait(false);

        return commitDto?.ToDomain();
    }

    private static string TrimText(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 0);

        var oneLine = value.ReplaceLineEndings(" ").Trim();
        if (oneLine.Length <= maxLength)
        {
            return oneLine;
        }

        return oneLine[..maxLength] + "...";
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpRetryExecutor _httpRetryExecutor;
    private readonly IResponseSerializer _responseSerializer;
    private readonly BitbucketOptions _bitbucketOptions;
}
