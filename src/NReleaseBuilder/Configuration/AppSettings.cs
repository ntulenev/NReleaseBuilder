namespace NReleaseBuilder.Configuration;

public sealed class AppSettings
{
    public string CsvFilePath { get; set; } = string.Empty;
    public BitbucketOptions Bitbucket { get; set; } = new();
    public JiraOptions Jira { get; set; } = new();
}
