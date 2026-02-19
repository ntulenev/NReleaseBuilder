using System.Text;

using FluentAssertions;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Presentation.Pdf;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NReleaseBuilder.Tests.Presentation.Pdf;

public class PdfContentComposerTests
{
    [Fact(DisplayName = "PdfContentComposer can be created.")]
    [Trait("Category", "Unit")]
    public void PdfContentComposerCanBeCreated()
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => _ = new PdfContentComposer());

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "PdfContentComposer can be created with options.")]
    [Trait("Category", "Unit")]
    public void PdfContentComposerCanBeCreatedWithOptions()
    {
        // Arrange
        var options = Options.Create(CreateAppSettings("https://jira.example.test"));

        // Act
        var exception = Record.Exception(() => _ = new PdfContentComposer(options));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "PdfContentComposer ComposeContent throws when column is null.")]
    [Trait("Category", "Unit")]
    public void ComposeContentThrowsWhenColumnIsNull()
    {
        // Arrange
        var sut = new PdfContentComposer();

        // Act
        Action action = () => sut.ComposeContent(
            null!,
            [],
            [],
            new Dictionary<JiraStatusName, int>());

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("column");
    }

    [Fact(DisplayName = "PdfContentComposer ComposeContent can render empty-state section when rows are empty.")]
    [Trait("Category", "Unit")]
    public void ComposeContentCanRenderEmptyStateSectionWhenRowsAreEmpty()
    {
        // Arrange
        var sut = new PdfContentComposer();
        var rows = Array.Empty<ComponentCheckRow>();
        var allowedStatuses = new[] { new JiraStatusName("Done") };
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("In Progress")] = 3,
            [new JiraStatusName("Blocked")] = 2,
        };

        // Act
        var exception = Record.Exception(() =>
        {
            var pdfBytes = GeneratePdf(column =>
                sut.ComposeContent(column, rows, allowedStatuses, statusStatistics));
            pdfBytes.Should().NotBeEmpty();
        });

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "PdfContentComposer ComposeContent can render results section for non-empty rows.")]
    [Trait("Category", "Unit")]
    public void ComposeContentCanRenderResultsSectionForNonEmptyRows()
    {
        // Arrange
        var sut = new PdfContentComposer();
        var rows =
            new[]
            {
                CreateRow(
                    1,
                    "api",
                    "repo-api",
                    "1.0.0",
                    CheckStatus.Outdated,
                    "Has newer versions",
                    [
                        CreateVersion(
                            "1.1.0",
                            "APP-1",
                            "Task title",
                            "Done",
                            hasRequiredActions: true,
                            hasBreakingChanges: true,
                            hasDependencyIssues: true,
                            requiredActionsDetails: "Upgrade deployment",
                            breakingChangesDetails: "API contract changed"),
                    ]),
                CreateRow(
                    2,
                    "worker",
                    "repo-worker",
                    "2.0.0",
                    CheckStatus.UpToDate,
                    "Up to date",
                    []),
            };
        var allowedStatuses = new[] { new JiraStatusName("Done") };
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };

        // Act
        var exception = Record.Exception(() =>
        {
            var pdfBytes = GeneratePdf(column =>
                sut.ComposeContent(column, rows, allowedStatuses, statusStatistics));
            pdfBytes.Should().NotBeEmpty();
        });

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "PdfContentComposer ComposeContent renders JiraTask links when Jira base url is configured.")]
    [Trait("Category", "Unit")]
    public void ComposeContentRendersJiraTaskLinksWhenJiraBaseUrlIsConfigured()
    {
        // Arrange
        var options = Options.Create(CreateAppSettings("https://jira.example.test"));
        var sut = new PdfContentComposer(options);
        var rows =
            new[]
            {
                CreateRow(
                    1,
                    "api",
                    "repo-api",
                    "1.0.0",
                    CheckStatus.Outdated,
                    "Has newer versions",
                    [
                        CreateVersion(
                            "1.1.0",
                            "APP-1, APP-2",
                            "Task title",
                            "Done",
                            hasRequiredActions: false,
                            hasBreakingChanges: false,
                            hasDependencyIssues: false,
                            requiredActionsDetails: null,
                            breakingChangesDetails: null),
                    ]),
            };
        var allowedStatuses = new[] { new JiraStatusName("Done") };
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 2,
        };

        // Act
        var pdfBytes = GeneratePdf(column =>
            sut.ComposeContent(column, rows, allowedStatuses, statusStatistics));
        var pdfText = Encoding.ASCII.GetString(pdfBytes);

        // Assert
        pdfText.Should().Contain("https://jira.example.test/browse/APP-1");
        pdfText.Should().Contain("https://jira.example.test/browse/APP-2");
    }

    [Fact(DisplayName = "PdfContentComposer ComposeContent renders Jira links in BC or RA details.")]
    [Trait("Category", "Unit")]
    public void ComposeContentRendersJiraLinksInBcOrRaDetails()
    {
        // Arrange
        var options = Options.Create(CreateAppSettings("https://jira.example.test"));
        var sut = new PdfContentComposer(options);
        var rows =
            new[]
            {
                CreateRow(
                    1,
                    "api",
                    "repo-api",
                    "1.0.0",
                    CheckStatus.Outdated,
                    "Has newer versions",
                    [
                        CreateVersion(
                            "1.1.0",
                            "N/A",
                            "Task title",
                            "Done",
                            hasRequiredActions: false,
                            hasBreakingChanges: true,
                            hasDependencyIssues: false,
                            requiredActionsDetails: null,
                            breakingChangesDetails: "Need FE part ADF-13848 and https://jira.example.test/browse/ADF-13849"),
                    ]),
            };
        var allowedStatuses = new[] { new JiraStatusName("Done") };
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };

        // Act
        var pdfBytes = GeneratePdf(column =>
            sut.ComposeContent(column, rows, allowedStatuses, statusStatistics));
        var pdfText = Encoding.ASCII.GetString(pdfBytes);

        // Assert
        pdfText.Should().Contain("/URI (https://jira.example.test/browse/ADF-13848)");
        pdfText.Should().Contain("/URI (https://jira.example.test/browse/ADF-13849)");
    }

    private static byte[] GeneratePdf(Action<ColumnDescriptor> composeContent)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document
            .Create(container =>
            {
                _ = container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(10);
                    page.Content().Column(column => composeContent(column));
                });
            })
            .GeneratePdf();
    }

    private static ComponentCheckRow CreateRow(
        int index,
        string component,
        string repository,
        string currentVersion,
        CheckStatus status,
        string details,
        IReadOnlyList<VersionJiraRow> newerVersions) =>
        new(
            new ComponentCheckIndex(index),
            new ComponentName(component),
            new RepositoryName(repository),
            new VersionLabel(currentVersion),
            status,
            new RowDetails(details),
            newerVersions);

    private static VersionJiraRow CreateVersion(
        string version,
        string jiraTask,
        string jiraTitle,
        string jiraStatus,
        bool hasRequiredActions,
        bool hasBreakingChanges,
        bool hasDependencyIssues,
        string? requiredActionsDetails,
        string? breakingChangesDetails) =>
        new(
            new VersionLabel(version),
            new JiraTaskReference(jiraTask),
            new JiraTitleReference(jiraTitle),
            new JiraStatusReference(jiraStatus),
            [
                new JiraTaskAlertDetails(
                    new JiraTaskReference(jiraTask),
                    new JiraTitleReference(jiraTitle),
                    new JiraStatusReference(jiraStatus),
                    requiredActionsDetails,
                    breakingChangesDetails),
            ],
            hasRequiredActions,
            hasBreakingChanges,
            hasDependencyIssues);

    private static AppSettings CreateAppSettings(string jiraBaseUrl) =>
        new()
        {
            CsvFilePath = "components.csv",
            Bitbucket = new BitbucketOptions
            {
                BaseUrl = new Uri("https://api.bitbucket.org/2.0"),
                Workspace = "workspace",
                AuthEmail = "bitbucket@example.test",
                AuthApiToken = "token",
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri(jiraBaseUrl),
                Email = "jira@example.test",
                ApiToken = "token",
            },
            Pdf = new PdfOptions
            {
                Enabled = true,
                OutputPath = "report.pdf",
            },
        };
}
