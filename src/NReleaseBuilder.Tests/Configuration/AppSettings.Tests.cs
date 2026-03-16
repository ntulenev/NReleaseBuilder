using FluentAssertions;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Rendering;

namespace NReleaseBuilder.Tests.Configuration;

public class AppSettingsTests
{
    [Fact(DisplayName = "AppSettings initializes defaults and stores required values.")]
    [Trait("Category", "Unit")]
    public void InitializesDefaultsAndStoresRequiredValues()
    {
        // Arrange
        var settings = new AppSettings
        {
            DevCsvFilePath = "dev-components.csv",
            TargetCsvFilePath = "target-components.csv",
            Bitbucket = CreateBitbucketOptions(),
            Jira = CreateJiraOptions(),
        };

        // Act
        var devCsvPath = settings.DevCsvFilePath;
        var targetCsvPath = settings.TargetCsvFilePath;
        var filter = settings.CsvComponentNamesFilter;
        var pdf = settings.Pdf;
        var excel = settings.Excel;

        // Assert
        devCsvPath.Should().Be("dev-components.csv");
        targetCsvPath.Should().Be("target-components.csv");
        filter.Should().NotBeNull();
        filter.Should().BeEmpty();
        settings.Bitbucket.Should().NotBeNull();
        settings.Jira.Should().NotBeNull();
        pdf.Should().NotBeNull();
        excel.Should().NotBeNull();
    }

    [Fact(DisplayName = "BuildReportRuns returns single default run when no groups configured.")]
    [Trait("Category", "Unit")]
    public void BuildReportRunsReturnsSingleDefaultRunWhenNoGroupsConfigured()
    {
        // Arrange
        var settings = new AppSettings
        {
            DevCsvFilePath = "dev-components.csv",
            TargetCsvFilePath = "target-components.csv",
            Bitbucket = CreateBitbucketOptions(),
            Jira = CreateJiraOptions(),
        };

        // Act
        var reportRuns = settings.BuildReportRuns();

        // Assert
        reportRuns.Should().HaveCount(1);
        reportRuns[0].Should().Be(new ReportRunDefinition(null, null, null, null));
    }

    [Fact(DisplayName = "BuildReportRuns maps grouped settings to report run definitions.")]
    [Trait("Category", "Unit")]
    public void BuildReportRunsMapsGroupedSettingsToReportRunDefinitions()
    {
        // Arrange
        var settings = new AppSettings
        {
            DevCsvFilePath = "dev-components.csv",
            TargetCsvFilePath = "target-components.csv",
            Bitbucket = CreateBitbucketOptions(),
            Jira = CreateJiraOptions(),
            CsvComponentGroups =
            [
                new CsvComponentGroupOptions
                {
                    Name = "  Backoffice  ",
                    ComponentNames = ["api-a", "api-b"],
                    PdfOutputPath = "backoffice.pdf",
                    ExcelOutputPath = "backoffice.xlsx",
                },
            ],
        };

        // Act
        var reportRuns = settings.BuildReportRuns();

        // Assert
        reportRuns.Should().HaveCount(1);
        reportRuns[0].Name.Should().Be("Backoffice");
        reportRuns[0].ComponentNamesFilter.Should().Equal("api-a", "api-b");
        reportRuns[0].PdfOutputPathOverride.Should().Be("backoffice.pdf");
        reportRuns[0].ExcelOutputPathOverride.Should().Be("backoffice.xlsx");
    }

    private static BitbucketOptions CreateBitbucketOptions() =>
        new()
        {
            BaseUrl = new Uri("https://bitbucket.example.test/"),
            Workspace = "workspace",
            ProjectNames = ["PROJ"],
            AuthEmail = "bot@example.test",
            AuthApiToken = "token",
        };

    private static JiraOptions CreateJiraOptions() =>
        new()
        {
            BaseUrl = new Uri("https://jira.example.test/"),
            Email = "jira@example.test",
            ApiToken = "token",
        };
}
