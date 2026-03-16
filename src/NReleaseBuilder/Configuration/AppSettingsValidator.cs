using Microsoft.Extensions.Options;

using NReleaseBuilder.Models.Bitbucket;



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

        ValidateCsvPaths(options.TargetCsvFilePath, options.DevCsvFilePath, errors);
        ValidateCsvComponentNamesFilter(options.CsvComponentNamesFilter, errors);
        ValidateCsvComponentGroups(options.CsvComponentGroups, options.Pdf, options.Excel, errors);
        ValidateBitbucket(options.Bitbucket, errors);
        ValidateJira(options.Jira, errors);
        ValidatePdf(options.Pdf, errors);
        ValidateExcel(options.Excel, errors);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateCsvPaths(
        string targetCsvFilePath,
        string devCsvFilePath,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(targetCsvFilePath))
        {
            errors.Add("TargetCsvFilePath is missing in appsettings.json.");
        }
        else if (!File.Exists(targetCsvFilePath))
        {
            errors.Add($"Target CSV file not found: {targetCsvFilePath}");
        }

        if (string.IsNullOrWhiteSpace(devCsvFilePath))
        {
            errors.Add("DevCsvFilePath is missing in appsettings.json.");
            return;
        }

        if (!File.Exists(devCsvFilePath))
        {
            errors.Add($"Dev CSV file not found: {devCsvFilePath}");
        }
    }

    private static void ValidateCsvComponentNamesFilter(IReadOnlyList<string>? componentNamesFilter, List<string> errors)
    {
        if (componentNamesFilter is null)
        {
            errors.Add("CsvComponentNamesFilter must not be null.");
            return;
        }

        if (componentNamesFilter.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("CsvComponentNamesFilter must not contain empty values.");
        }
    }

    private static void ValidateCsvComponentGroups(
        IReadOnlyList<CsvComponentGroupOptions>? componentGroups,
        PdfOptions? pdf,
        ExcelOptions? excel,
        List<string> errors)
    {
        if (componentGroups is null)
        {
            errors.Add("CsvComponentGroups must not be null.");
            return;
        }

        if (componentGroups.Count == 0)
        {
            return;
        }

        var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var groupIndex = 0; groupIndex < componentGroups.Count; groupIndex++)
        {
            var group = componentGroups[groupIndex];

            if (string.IsNullOrWhiteSpace(group.Name))
            {
                errors.Add($"CsvComponentGroups[{groupIndex}].Name must not be empty.");
            }
            else if (!groupNames.Add(group.Name.Trim()))
            {
                errors.Add($"CsvComponentGroups contains duplicate group name: '{group.Name.Trim()}'.");
            }

            if (group.ComponentNames is null)
            {
                errors.Add($"CsvComponentGroups[{groupIndex}].ComponentNames must not be null.");
            }
            else
            {
                if (group.ComponentNames.Count == 0)
                {
                    errors.Add($"CsvComponentGroups[{groupIndex}].ComponentNames must contain at least one component name.");
                }

                if (group.ComponentNames.Any(string.IsNullOrWhiteSpace))
                {
                    errors.Add($"CsvComponentGroups[{groupIndex}].ComponentNames must not contain empty values.");
                }
            }

            if (pdf?.Enabled == true)
            {
                ValidateGroupOutputPath(
                    group.PdfOutputPath,
                    $"CsvComponentGroups[{groupIndex}].PdfOutputPath",
                    "Pdf",
                    (outputPath) => pdf.ResolveOutputPath(outputPath),
                    errors);
            }

            if (excel?.Enabled == true)
            {
                ValidateGroupOutputPath(
                    group.ExcelOutputPath,
                    $"CsvComponentGroups[{groupIndex}].ExcelOutputPath",
                    "Excel",
                    (outputPath) => excel.ResolveOutputPath(outputPath),
                    errors);
            }
        }
    }

    private static void ValidateGroupOutputPath(
        string? outputPath,
        string groupPathKey,
        string reportType,
        Func<string, string> resolver,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            errors.Add($"{groupPathKey} is required when {reportType}.Enabled is true.");
            return;
        }

        try
        {
            _ = resolver(outputPath);
        }
        catch (ArgumentException)
        {
            errors.Add($"{groupPathKey} is invalid: '{outputPath}'.");
        }
        catch (NotSupportedException)
        {
            errors.Add($"{groupPathKey} is not supported: '{outputPath}'.");
        }
        catch (PathTooLongException)
        {
            errors.Add($"{groupPathKey} is too long.");
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

    private static void ValidateExcel(ExcelOptions? excel, List<string> errors)
    {
        if (excel is null)
        {
            errors.Add("Excel section is missing in appsettings.json.");
            return;
        }

        if (!excel.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(excel.OutputPath))
        {
            errors.Add("Excel.OutputPath is required when Excel.Enabled is true.");
            return;
        }

        try
        {
            _ = excel.ResolveOutputPath();
        }
        catch (ArgumentException)
        {
            errors.Add($"Excel.OutputPath is invalid: '{excel.OutputPath}'.");
        }
        catch (NotSupportedException)
        {
            errors.Add($"Excel.OutputPath is not supported: '{excel.OutputPath}'.");
        }
        catch (PathTooLongException)
        {
            errors.Add("Excel.OutputPath is too long.");
        }
    }
}
