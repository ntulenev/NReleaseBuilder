using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Abstractions.Application;

/// <summary>
/// Builds component check rows from normalized component rows and repository context.
/// </summary>
public interface IComponentsVersionBuilder
{
    /// <summary>
    /// Loads repository data and builds check rows.
    /// </summary>
    /// <param name="normalizedComponentRows">Normalized component rows.</param>
    /// <param name="repositoryContext">Repository context built from rows.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Calculated check rows; <see langword="null"/> when loading fails.</returns>
    Task<IReadOnlyList<ComponentCheckRow>?> BuildAsync(
        IReadOnlyList<ComponentRow> normalizedComponentRows,
        RepositoryVersionContext repositoryContext,
        CancellationToken cancellationToken);
}
