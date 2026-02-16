namespace NReleaseBuilder.Models;

public enum CheckStatus
{
    UpToDate,
    Outdated,
    RepositoryNotFound,
    BitbucketError,
    InvalidCurrentVersion,
}
