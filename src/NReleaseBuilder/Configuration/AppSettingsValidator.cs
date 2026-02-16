using Microsoft.Extensions.Options;

using NReleaseBuilder.Models;

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
        ValidatePdf(options.Pdf, errors);

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

        if (bitbucket.ProjectNames.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Bitbucket.ProjectNames must not contain empty values.");
        }

        foreach (var (sourceRepositoryName, targetRepositoryName) in bitbucket.RepositoryNameOverrides)
        {
            if (!RepositoryName.TryCreate(sourceRepositoryName, out _))
            {
                errors.Add($"Bitbucket.RepositoryNameOverrides contains invalid source repository key: '{sourceRepositoryName}'.");
            }

            if (!RepositoryName.TryCreate(targetRepositoryName, out _))
            {
                errors.Add($"Bitbucket.RepositoryNameOverrides contains invalid target repository value for '{sourceRepositoryName}'.");
            }
        }

        var projectNames = bitbucket.ResolveProjectNames();
        if (projectNames.Length == 0)
        {
            errors.Add("Bitbucket.ProjectNames must contain at least one value (ProjectName alias is accepted).");
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

        if (jira.AllowedTaskStatuses is null)
        {
            errors.Add("Jira.AllowedTaskStatuses must not be null.");
            return;
        }

        if (jira.AllowedTaskStatuses.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Jira.AllowedTaskStatuses must not contain empty values.");
        }

        if (string.IsNullOrWhiteSpace(jira.RequiredActionsFieldName))
        {
            errors.Add("Jira.RequiredActionsFieldName must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(jira.BreakingChangesFieldName))
        {
            errors.Add("Jira.BreakingChangesFieldName must not be empty.");
        }
    }

    private static void ValidatePdf(PdfOptions? pdf, List<string> errors)
    {
        if (pdf is null)
        {
            errors.Add("Pdf section is missing in appsettings.json.");
            return;
        }

        if (!pdf.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(pdf.OutputPath))
        {
            errors.Add("Pdf.OutputPath is required when Pdf.Enabled is true.");
            return;
        }

        try
        {
            _ = pdf.ResolveOutputPath();
        }
        catch (ArgumentException)
        {
            errors.Add($"Pdf.OutputPath is invalid: '{pdf.OutputPath}'.");
        }
        catch (NotSupportedException)
        {
            errors.Add($"Pdf.OutputPath is not supported: '{pdf.OutputPath}'.");
        }
        catch (PathTooLongException)
        {
            errors.Add("Pdf.OutputPath is too long.");
        }
    }
}
