using FluentAssertions;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Models.Rendering;
using NReleaseBuilder.Presentation.Excel;

namespace NReleaseBuilder.Tests.Presentation.Excel;

public class MiniExcelContentComposerTests
{
    [Fact(DisplayName = "MiniExcelContentComposer can be created with options.")]
    [Trait("Category", "Unit")]
    public void MiniExcelContentComposerCanBeCreatedWithOptions()
    {
        // Arrange
        var options = Options.Create(CreateSettings());

        // Act
        var exception = Record.Exception(() => _ = new MiniExcelContentComposer(options));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "MiniExcelContentComposer ComposeWorkbook validates null arguments.")]
    [Trait("Category", "Unit")]
    public void ComposeWorkbookValidatesNullArguments()
    {
        // Arrange
        var sut = new MiniExcelContentComposer(Options.Create(CreateSettings()));

        // Act
        Action nullRows = () => sut.ComposeWorkbook(null!, [], CreateStatistics());
        Action nullStatuses = () => sut.ComposeWorkbook([], null!, CreateStatistics());
        Action nullStatistics = () => sut.ComposeWorkbook([], [], null!);

        // Assert
        nullRows.Should().Throw<ArgumentNullException>()
            .WithParameterName("rows");
        nullStatuses.Should().Throw<ArgumentNullException>()
            .WithParameterName("allowedStatuses");
        nullStatistics.Should().Throw<ArgumentNullException>()
            .WithParameterName("statusStatistics");
    }

    [Fact(DisplayName = "MiniExcelContentComposer creates summary and component workbook content with links and alert sections.")]
    [Trait("Category", "Unit")]
    public void ComposeWorkbookCreatesExpectedWorkbookContent()
    {
        // Arrange
        var sut = new MiniExcelContentComposer(Options.Create(CreateSettings()));
        var rows = new[] { CreateRow() };
        var statuses = new[] { new JiraStatusName("Done") };
        var statistics = CreateStatistics();

        // Act
        var workbook = sut.ComposeWorkbook(rows, statuses, statistics);

        // Assert
        workbook.Sheets.Should().HaveCount(2);
        workbook.Sheets.Keys.Should().Contain("Summary");
        workbook.Sheets.Keys.Should().ContainSingle(name => name.Contains("component-api", StringComparison.OrdinalIgnoreCase));

        var summaryRows = workbook.Sheets["Summary"].Should().BeAssignableTo<List<Dictionary<string, object?>>>().Subject;
        summaryRows.Any(row => string.Equals(row["C1"]?.ToString(), "Components Version Check", StringComparison.Ordinal)).Should().BeTrue();
        summaryRows.Any(row => string.Equals(row["C1"]?.ToString(), "Results", StringComparison.Ordinal)).Should().BeTrue();
        summaryRows.Any(row => string.Equals(row["C1"]?.ToString(), "Unique Jira Tasks By Status", StringComparison.Ordinal)).Should().BeTrue();

        var componentSheetName = workbook.Sheets.Keys.Single(name => !string.Equals(name, "Summary", StringComparison.Ordinal));
        var componentRows = workbook.Sheets[componentSheetName].Should().BeAssignableTo<List<Dictionary<string, object?>>>().Subject;
        componentRows.Any(row => string.Equals(row["C1"]?.ToString(), "1. component-api", StringComparison.Ordinal)).Should().BeTrue();
        componentRows.Any(row => string.Equals(row["C1"]?.ToString(), "Breaking Changes", StringComparison.Ordinal)).Should().BeTrue();
        componentRows.Any(row => string.Equals(row["C1"]?.ToString(), "Required Actions", StringComparison.Ordinal)).Should().BeTrue();
        componentRows.Any(row => string.Equals(row["C5"]?.ToString(), "RA BC D", StringComparison.Ordinal)).Should().BeTrue();

        var componentLayout = workbook.Layouts[componentSheetName];
        componentLayout.Should().BeOfType<ExcelSheetLayout>();
        componentLayout.Hyperlinks.Values.Should().Contain("https://bitbucket.example.test/projects/PROJ/repos/repo/pull-requests/42");
        componentLayout.Hyperlinks.Values.Should().Contain("https://jira.example.test/browse/APP-42");
        componentLayout.Comments.Should().HaveCount(2);
        componentLayout.Comments.Values.Should().OnlyContain(value => value.Contains("Contains Jira link:", StringComparison.Ordinal));
    }

    private static AppSettings CreateSettings() =>
        new()
        {
            DevCsvFilePath = "components.csv",
            TargetCsvFilePath = "components.csv",
            CsvComponentNamesFilter = [],
            Bitbucket = new BitbucketOptions
            {
                BaseUrl = new Uri("https://bitbucket.example.test/"),
                Workspace = "workspace",
                ProjectNames = ["PROJ"],
                AuthEmail = "bot@example.test",
                AuthApiToken = "token",
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
                Email = "jira@example.test",
                ApiToken = "token",
                AllowedTaskStatuses = ["Done"],
                RequiredActionsFieldName = "Required Actions",
                BreakingChangesFieldName = "Breaking changes",
            },
            Pdf = new PdfOptions
            {
                Enabled = false,
                OutputPath = "report.pdf",
            },
            Excel = new ExcelOptions
            {
                Enabled = true,
                OutputPath = "report.xlsx",
            },
        };

    private static Dictionary<JiraStatusName, int> CreateStatistics() =>
        new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };

    private static ComponentCheckRow CreateRow()
    {
        var jiraTask = new JiraTaskReference("APP-42");
        var jiraStatus = new JiraStatusReference("Done");
        var jiraTitle = new JiraTitleReference("Improve release report");

        var version = new VersionJiraRow(
            new VersionLabel("2.0.0"),
            jiraTask,
            jiraTitle,
            jiraStatus,
            [
                new JiraTaskAlertDetails(
                    jiraTask,
                    jiraTitle,
                    jiraStatus,
                    "Review deployment steps for APP-42",
                    "Breaking change details in https://jira.example.test/browse/APP-42"),
            ],
            hasRequiredActions: true,
            hasBreakingChanges: true,
            hasDependencyIssues: true,
            pullRequestUrl: new Uri("https://bitbucket.example.test/projects/PROJ/repos/repo/pull-requests/42"));

        return new ComponentCheckRow(
            new ComponentCheckIndex(1),
            new ComponentName("component-api"),
            new RepositoryName("repo-api"),
            new VersionLabel("1.0.0"),
            CheckStatus.Outdated,
            RowDetails.CreatePlaceholder(),
            [version]);
    }
}
