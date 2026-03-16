using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Bitbucket;

/// <summary>
/// Applies Bitbucket repository override rules to component rows.
/// </summary>
public sealed class RepositoryNameNormalizer : IRepositoryNameNormalizer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryNameNormalizer"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    public RepositoryNameNormalizer(IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _bitbucketOptions = options.Value.Bitbucket;
    }

    /// <inheritdoc />
    public IReadOnlyList<ComponentRow> Normalize(IReadOnlyList<ComponentRow> componentRows)
    {
        ArgumentNullException.ThrowIfNull(componentRows);

        if (_bitbucketOptions.RepositoryNameOverrides.Count == 0)
        {
            return componentRows;
        }

        var normalizedRows = new List<ComponentRow>(componentRows.Count);

        foreach (var row in componentRows)
        {
            normalizedRows.Add(new ComponentRow(
                row.Component,
                _bitbucketOptions.ResolveRepositoryName(row.Repository),
                row.Version,
                row.IsReleased));
        }

        return normalizedRows;
    }

    private readonly BitbucketOptions _bitbucketOptions;
}
