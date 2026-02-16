using System.Text.Json;

namespace NReleaseBuilder.Configuration;

public sealed class AppSettingsLoader
{
    private const string SettingsFileName = "appsettings.json";

    public bool TryLoad(out AppSettings settings, out string? error)
    {
        settings = new AppSettings();
        error = null;

        var settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        if (!File.Exists(settingsPath))
        {
            error = $"Configuration file not found: {settingsPath}";
            return false;
        }

        AppSettings? parsedSettings;
        try
        {
            parsedSettings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(settingsPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            error = $"Failed to read configuration: {ex.Message}";
            return false;
        }

        if (parsedSettings is null)
        {
            error = "Configuration file has invalid JSON structure.";
            return false;
        }

        error = Validate(parsedSettings);
        settings = parsedSettings;
        return error is null;
    }

    public static string? Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.CsvFilePath))
        {
            return "CsvFilePath is missing in appsettings.json.";
        }

        if (!File.Exists(settings.CsvFilePath))
        {
            return $"CSV file not found: {settings.CsvFilePath}";
        }

        if (settings.Bitbucket is null)
        {
            return "Bitbucket section is missing in appsettings.json.";
        }

        if (string.IsNullOrWhiteSpace(settings.Bitbucket.BaseUrl))
        {
            return "Bitbucket.BaseUrl is required.";
        }

        if (!Uri.TryCreate(settings.Bitbucket.BaseUrl, UriKind.Absolute, out _))
        {
            return $"Bitbucket.BaseUrl is not a valid absolute URL: {settings.Bitbucket.BaseUrl}";
        }

        if (string.IsNullOrWhiteSpace(settings.Bitbucket.Workspace))
        {
            return "Bitbucket.Workspace is required.";
        }

        if (string.IsNullOrWhiteSpace(settings.Bitbucket.ProjectName))
        {
            return "Bitbucket.ProjectName is required (example: AAA).";
        }

        if (string.IsNullOrWhiteSpace(settings.Bitbucket.AuthEmail))
        {
            return "Bitbucket.AuthEmail is required.";
        }

        if (string.IsNullOrWhiteSpace(settings.Bitbucket.AuthApiToken))
        {
            return "Bitbucket.AuthApiToken is required.";
        }

        if (settings.Bitbucket.PageLen is < 1 or > 100)
        {
            return "Bitbucket.PageLen must be between 1 and 100.";
        }

        if (settings.Bitbucket.RetryCount is < 0 or > 10)
        {
            return "Bitbucket.RetryCount must be between 0 and 10.";
        }

        if (settings.Bitbucket.MaxParallelRequests is < 1 or > 20)
        {
            return "Bitbucket.MaxParallelRequests must be between 1 and 20.";
        }

        if (settings.Jira is null)
        {
            return "Jira section is missing in appsettings.json.";
        }

        if (string.IsNullOrWhiteSpace(settings.Jira.BaseUrl))
        {
            return "Jira.BaseUrl is required.";
        }

        if (!Uri.TryCreate(settings.Jira.BaseUrl, UriKind.Absolute, out _))
        {
            return $"Jira.BaseUrl is not a valid absolute URL: {settings.Jira.BaseUrl}";
        }

        var jiraEmail = ResolveJiraEmail(settings.Jira);
        var jiraToken = ResolveJiraToken(settings.Jira);
        var jiraEmailMissing = string.IsNullOrWhiteSpace(jiraEmail);
        var jiraTokenMissing = string.IsNullOrWhiteSpace(jiraToken);
        if (jiraEmailMissing != jiraTokenMissing)
        {
            return "Jira.Email and Jira.ApiToken must be both provided (AuthEmail/AuthApiToken are accepted aliases).";
        }

        if (jiraEmailMissing)
        {
            return "Jira.Email is required.";
        }

        if (settings.Jira.RetryCount is < 0 or > 10)
        {
            return "Jira.RetryCount must be between 0 and 10.";
        }

        if (settings.Jira.AllowedTaskStatuses is null || settings.Jira.AllowedTaskStatuses.Count == 0)
        {
            return "Jira.AllowedTaskStatuses must contain at least one status.";
        }

        if (settings.Jira.AllowedTaskStatuses.Any(string.IsNullOrWhiteSpace))
        {
            return "Jira.AllowedTaskStatuses must not contain empty values.";
        }

        if (settings.Jira.MaxParallelRequests is < 1 or > 20)
        {
            return "Jira.MaxParallelRequests must be between 1 and 20.";
        }

        return null;
    }

    private static string ResolveJiraEmail(JiraOptions jira)
    {
        if (!string.IsNullOrWhiteSpace(jira.Email))
        {
            return jira.Email.Trim();
        }

        return jira.AuthEmail.Trim();
    }

    private static string ResolveJiraToken(JiraOptions jira)
    {
        if (!string.IsNullOrWhiteSpace(jira.ApiToken))
        {
            return jira.ApiToken.Trim();
        }

        return jira.AuthApiToken.Trim();
    }
}
