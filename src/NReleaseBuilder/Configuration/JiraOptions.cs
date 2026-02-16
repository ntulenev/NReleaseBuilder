namespace NReleaseBuilder.Configuration;

public sealed class JiraOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public IReadOnlyList<string> AllowedTaskStatuses { get; set; } = [];
    public int MaxParallelRequests { get; set; } = 2;

    // Backward-compatible aliases.
    public string AuthEmail { get; set; } = string.Empty;
    public string AuthApiToken { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 2;
}
