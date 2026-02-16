namespace NReleaseBuilder.Models;

public readonly record struct RepositoryTagLookup(bool IsRepositoryMissing, string? Error, IReadOnlyList<RepositoryTagInfo> Tags)
{
    public static RepositoryTagLookup RepoNotFound() => new(true, null, Array.Empty<RepositoryTagInfo>());

    public static RepositoryTagLookup ApiError(string error) => new(false, error, Array.Empty<RepositoryTagInfo>());

    public static RepositoryTagLookup Success(IReadOnlyList<RepositoryTagInfo> tags) => new(false, null, tags);
}
