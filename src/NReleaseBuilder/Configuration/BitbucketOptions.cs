namespace NReleaseBuilder.Configuration;

public sealed class BitbucketOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string AuthEmail { get; set; } = string.Empty;
    public string AuthApiToken { get; set; } = string.Empty;
    public int PageLen { get; set; } = 50;
    public int RetryCount { get; set; } = 2;
    public int MaxParallelRequests { get; set; } = 6;
}
