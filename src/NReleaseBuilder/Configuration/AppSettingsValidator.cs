using Microsoft.Extensions.Options;

namespace NReleaseBuilder.Configuration;

/// <summary>
/// Additional validation for <see cref="AppSettings"/> that depends on multiple fields.
/// </summary>
public sealed class AppSettingsValidator : IValidateOptions<AppSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AppSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        ValidateCsvPath(options.CsvFilePath, errors);
        ValidateBitbucket(options.Bitbucket, errors);
        ValidateJira(options.Jira, errors);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateCsvPath(string csvFilePath, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(csvFilePath))
        {
            errors.Add("CsvFilePath is missing in appsettings.json.");
            return;
        }

        if (!File.Exists(csvFilePath))
        {
            errors.Add($"CSV file not found: {csvFilePath}");
        }
    }

    private static void ValidateBitbucket(BitbucketOptions? bitbucket, List<string> errors)
    {
        if (bitbucket is null)
        {
            errors.Add("Bitbucket section is missing in appsettings.json.");
            return;
        }

        if (!bitbucket.BaseUrl.IsAbsoluteUri)
        {
            errors.Add($"Bitbucket.BaseUrl is not a valid absolute URL: {bitbucket.BaseUrl}");
        }
    }

    private static void ValidateJira(JiraOptions? jira, List<string> errors)
    {
        if (jira is null)
        {
            errors.Add("Jira section is missing in appsettings.json.");
            return;
        }

        if (!jira.BaseUrl.IsAbsoluteUri)
        {
            errors.Add($"Jira.BaseUrl is not a valid absolute URL: {jira.BaseUrl}");
        }

        var jiraEmail = jira.ResolveAuthEmail();
        var jiraToken = jira.ResolveAuthApiToken();

        var jiraEmailMissing = string.IsNullOrWhiteSpace(jiraEmail);
        var jiraTokenMissing = string.IsNullOrWhiteSpace(jiraToken);

        if (jiraEmailMissing != jiraTokenMissing)
        {
            errors.Add(
                "Jira.Email and Jira.ApiToken must be both provided (AuthEmail/AuthApiToken are accepted aliases).");
        }
        else if (jiraEmailMissing)
        {
            errors.Add("Jira.Email is required.");
        }

        if (jira.AllowedTaskStatuses is null || jira.AllowedTaskStatuses.Count == 0)
        {
            errors.Add("Jira.AllowedTaskStatuses must contain at least one status.");
            return;
        }

        if (jira.AllowedTaskStatuses.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Jira.AllowedTaskStatuses must not contain empty values.");
        }
    }
}
