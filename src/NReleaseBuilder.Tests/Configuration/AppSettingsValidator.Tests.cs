using FluentAssertions;

using NReleaseBuilder.Configuration;

namespace NReleaseBuilder.Tests.Configuration;

public class AppSettingsValidatorTests
{
    [Fact(DisplayName = "AppSettingsValidator Validate throws when options are null.")]
    [Trait("Category", "Unit")]
    public void ValidateThrowsWhenOptionsAreNull()
    {
        // Arrange
        var sut = new AppSettingsValidator();

        // Act
        Action action = () => _ = sut.Validate(null, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate succeeds for valid settings.")]
    [Trait("Category", "Unit")]
    public void ValidateSucceedsForValidSettings()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var settings = CreateValidSettings(csvFile.Path);

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails when csv path is missing.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsWhenCsvPathIsMissing()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var settings = new AppSettings
        {
            CsvFilePath = " ",
            CsvComponentNamesFilter = ["api"],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = CreateValidJiraOptions(),
            Pdf = CreateValidPdfOptions(),
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("CsvFilePath is missing in appsettings.json.");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails when csv file does not exist.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsWhenCsvFileDoesNotExist()
    {
        // Arrange
        var sut = new AppSettingsValidator();
        var settings = CreateValidSettings(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv"));

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(x => x.StartsWith("CSV file not found:", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails for invalid component name filter values.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsForInvalidComponentNameFilterValues()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var settingsWithNullFilter = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = null!,
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = CreateValidJiraOptions(),
            Pdf = CreateValidPdfOptions(),
        };
        var settingsWithEmptyFilter = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api", " "],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = CreateValidJiraOptions(),
            Pdf = CreateValidPdfOptions(),
        };

        // Act
        var nullResult = sut.Validate(null, settingsWithNullFilter);
        var emptyResult = sut.Validate(null, settingsWithEmptyFilter);

        // Assert
        nullResult.Failed.Should().BeTrue();
        nullResult.Failures.Should().Contain("CsvComponentNamesFilter must not be null.");
        emptyResult.Failed.Should().BeTrue();
        emptyResult.Failures.Should().Contain("CsvComponentNamesFilter must not contain empty values.");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails when bitbucket section is missing.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsWhenBitbucketSectionIsMissing()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var settings = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = null!,
            Jira = CreateValidJiraOptions(),
            Pdf = CreateValidPdfOptions(),
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Bitbucket section is missing in appsettings.json.");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails for invalid bitbucket values.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsForInvalidBitbucketValues()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var invalidBitbucket = new BitbucketOptions
        {
            BaseUrl = new Uri("/relative", UriKind.Relative),
            Workspace = "workspace",
            ProjectNames = [" "],
            ProjectName = " ",
            AuthEmail = "bot@example.test",
            AuthApiToken = "token",
            RepositoryNameOverrides = new Dictionary<string, string>
            {
                [" "] = "repo-a",
                ["repo-b"] = " ",
            },
        };
        var settings = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = invalidBitbucket,
            Jira = CreateValidJiraOptions(),
            Pdf = CreateValidPdfOptions(),
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(x => x.StartsWith("Bitbucket.BaseUrl is not a valid absolute URL:", StringComparison.Ordinal));
        result.Failures.Should().Contain("Bitbucket.ProjectNames must not contain empty values.");
        result.Failures.Should().Contain(x => x.Contains("invalid source repository key", StringComparison.Ordinal));
        result.Failures.Should().Contain(x => x.Contains("invalid target repository value", StringComparison.Ordinal));
        result.Failures.Should().Contain("Bitbucket.ProjectNames must contain at least one value (ProjectName alias is accepted).");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails when jira section is missing.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsWhenJiraSectionIsMissing()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var settings = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = null!,
            Pdf = CreateValidPdfOptions(),
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Jira section is missing in appsettings.json.");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails for invalid jira values.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsForInvalidJiraValues()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var invalidJira = new JiraOptions
        {
            BaseUrl = new Uri("/relative", UriKind.Relative),
            Email = "jira@example.test",
            ApiToken = " ",
            AllowedTaskStatuses = ["Done", " "],
            RequiredActionsFieldName = " ",
            BreakingChangesFieldName = "",
        };
        var settings = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = invalidJira,
            Pdf = CreateValidPdfOptions(),
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(x => x.StartsWith("Jira.BaseUrl is not a valid absolute URL:", StringComparison.Ordinal));
        result.Failures.Should().Contain("Jira.Email and Jira.ApiToken must be both provided (AuthEmail/AuthApiToken are accepted aliases).");
        result.Failures.Should().Contain("Jira.AllowedTaskStatuses must not contain empty values.");
        result.Failures.Should().Contain("Jira.RequiredActionsFieldName must not be empty.");
        result.Failures.Should().Contain("Jira.BreakingChangesFieldName must not be empty.");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails when allowed jira statuses are null.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsWhenAllowedJiraStatusesAreNull()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var jira = new JiraOptions
        {
            BaseUrl = new Uri("https://jira.example.test/"),
            Email = "jira@example.test",
            ApiToken = "token",
            AllowedTaskStatuses = null!,
        };
        var settings = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = jira,
            Pdf = CreateValidPdfOptions(),
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Jira.AllowedTaskStatuses must not be null.");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails when pdf section is missing.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsWhenPdfSectionIsMissing()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var settings = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = CreateValidJiraOptions(),
            Pdf = null!,
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Pdf section is missing in appsettings.json.");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate fails when pdf output path is empty and pdf enabled.")]
    [Trait("Category", "Unit")]
    public void ValidateFailsWhenPdfOutputPathIsEmptyAndPdfEnabled()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var settings = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = CreateValidJiraOptions(),
            Pdf = new PdfOptions
            {
                Enabled = true,
                OutputPath = " ",
            },
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Pdf.OutputPath is required when Pdf.Enabled is true.");
    }

    [Fact(DisplayName = "AppSettingsValidator Validate allows empty pdf output path when pdf disabled.")]
    [Trait("Category", "Unit")]
    public void ValidateAllowsEmptyPdfOutputPathWhenPdfDisabled()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile();
        var sut = new AppSettingsValidator();
        var settings = new AppSettings
        {
            CsvFilePath = csvFile.Path,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = CreateValidJiraOptions(),
            Pdf = new PdfOptions
            {
                Enabled = false,
                OutputPath = " ",
            },
        };

        // Act
        var result = sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    private static AppSettings CreateValidSettings(string csvPath) =>
        new()
        {
            CsvFilePath = csvPath,
            CsvComponentNamesFilter = ["api"],
            Bitbucket = CreateValidBitbucketOptions(),
            Jira = CreateValidJiraOptions(),
            Pdf = CreateValidPdfOptions(),
        };

    private static BitbucketOptions CreateValidBitbucketOptions() =>
        new()
        {
            BaseUrl = new Uri("https://bitbucket.example.test/"),
            Workspace = "workspace",
            ProjectNames = ["PROJ"],
            AuthEmail = "bot@example.test",
            AuthApiToken = "token",
        };

    private static JiraOptions CreateValidJiraOptions() =>
        new()
        {
            BaseUrl = new Uri("https://jira.example.test/"),
            Email = "jira@example.test",
            ApiToken = "token",
            AllowedTaskStatuses = ["Done"],
            RequiredActionsFieldName = "Required Actions",
            BreakingChangesFieldName = "Breaking changes",
        };

    private static PdfOptions CreateValidPdfOptions() =>
        new()
        {
            Enabled = true,
            OutputPath = "report.pdf",
        };

    private static TempCsvFile CreateTempCsvFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "component,repository,version");
        return new TempCsvFile(path);
    }

    private sealed class TempCsvFile(string path) : IDisposable
    {
        public string Path { get; } = path;

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
