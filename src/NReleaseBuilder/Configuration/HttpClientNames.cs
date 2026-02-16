namespace NReleaseBuilder.Configuration;

/// <summary>
/// Named <see cref="HttpClient"/> registrations used by application services.
/// </summary>
public static class HttpClientNames
{
    /// <summary>
    /// Named Bitbucket HTTP client.
    /// </summary>
    public const string BITBUCKET = "Bitbucket";

    /// <summary>
    /// Named Jira HTTP client.
    /// </summary>
    public const string JIRA = "Jira";
}
