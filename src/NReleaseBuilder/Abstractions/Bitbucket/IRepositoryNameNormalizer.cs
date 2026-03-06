using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Abstractions.Bitbucket;

/// <summary>
/// Normalizes repository names in component rows.
/// </summary>
public interface IRepositoryNameNormalizer
{
    /// <summary>
    /// Applies repository-name normalization rules to component rows.
    /// </summary>
    /// <param name="componentRows">Rows to normalize.</param>
    /// <returns>Normalized rows.</returns>
    IReadOnlyList<ComponentRow> Normalize(IReadOnlyList<ComponentRow> componentRows);
}
