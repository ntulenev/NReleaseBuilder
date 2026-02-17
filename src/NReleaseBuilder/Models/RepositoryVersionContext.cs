using NReleaseBuilder.Services;

using NuGet.Versioning;

namespace NReleaseBuilder.Models;

/// <summary>
/// Repository-level context derived from component rows.
/// </summary>
public sealed class RepositoryVersionContext
{
    private RepositoryVersionContext(
        IReadOnlyList<RepositoryName> repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minCurrentVersionsByRepository)
    {
        Repositories = repositories;
        MinCurrentVersionsByRepository = minCurrentVersionsByRepository;
    }

    /// <summary>
    /// Distinct repositories found in CSV component rows.
    /// </summary>
    public IReadOnlyList<RepositoryName> Repositories { get; }

    /// <summary>
    /// Minimum parsed current version per repository.
    /// </summary>
    public IReadOnlyDictionary<RepositoryName, NuGetVersion> MinCurrentVersionsByRepository { get; }

    /// <summary>
    /// Builds repository context from parsed CSV component rows.
    /// </summary>
    /// <param name="componentRows">Parsed component rows.</param>
    /// <returns>Repository context used for tag loading.</returns>
    public static RepositoryVersionContext BuildRepositoryVersionContext(IReadOnlyList<ComponentRow> componentRows)
    {
        ArgumentNullException.ThrowIfNull(componentRows);

        var repositories = componentRows
            .Select(static row => row.Repository)
            .Distinct()
            .ToArray();

        var minCurrentVersionsByRepository = BuildMinCurrentVersionsByRepository(componentRows);
        return new RepositoryVersionContext(repositories, minCurrentVersionsByRepository);
    }

    private static Dictionary<RepositoryName, NuGetVersion> BuildMinCurrentVersionsByRepository(
        IReadOnlyList<ComponentRow> componentRows)
    {
        var minCurrentVersionsByRepository = new Dictionary<RepositoryName, NuGetVersion>();

        foreach (var row in componentRows)
        {
            if (!VersionParser.TryParse(row.Version, out var parsedVersion))
            {
                continue;
            }

            var repositoryName = row.Repository;

            if (!minCurrentVersionsByRepository.TryGetValue(repositoryName, out var minVersion)
                || parsedVersion < minVersion)
            {
                minCurrentVersionsByRepository[repositoryName] = parsedVersion;
            }
        }

        return minCurrentVersionsByRepository;
    }
}
