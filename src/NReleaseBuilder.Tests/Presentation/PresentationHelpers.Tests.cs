using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Presentation;

namespace NReleaseBuilder.Tests.Presentation;

public class PresentationHelpersTests
{
    [Fact(DisplayName = "PresentationHelpers BuildStatusFilterLabel returns fallback label for empty statuses.")]
    [Trait("Category", "Unit")]
    public void BuildStatusFilterLabelReturnsFallbackLabelForEmptyStatuses()
    {
        // Arrange
        IReadOnlyList<JiraStatusName> statuses = [];

        // Act
        var result = statuses.BuildStatusFilterLabel();

        // Assert
        result.Should().Be("configured statuses");
    }

    [Fact(DisplayName = "PresentationHelpers BuildStatusFilterLabel joins status names.")]
    [Trait("Category", "Unit")]
    public void BuildStatusFilterLabelJoinsStatusNames()
    {
        // Arrange
        IReadOnlyList<JiraStatusName> statuses = [new JiraStatusName("Done"), new JiraStatusName("In Progress")];

        // Act
        var result = statuses.BuildStatusFilterLabel();

        // Assert
        result.Should().Be("Done, In Progress");
    }

    [Fact(DisplayName = "PresentationHelpers BuildStatusFilterLabel throws for null statuses.")]
    [Trait("Category", "Unit")]
    public void BuildStatusFilterLabelThrowsForNullStatuses()
    {
        // Arrange
        IReadOnlyList<JiraStatusName>? statuses = null;

        // Act
        Action action = () => _ = statuses!.BuildStatusFilterLabel();

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("statuses");
    }

    [Fact(DisplayName = "PresentationHelpers BuildTopDisallowedStatusLabels returns sorted top labels.")]
    [Trait("Category", "Unit")]
    public void BuildTopDisallowedStatusLabelsReturnsSortedTopLabels()
    {
        // Arrange
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 2,
            [new JiraStatusName("Blocked")] = 7,
            [new JiraStatusName("Review")] = 7,
            [new JiraStatusName("In Progress")] = 5,
        };
        IReadOnlyCollection<JiraStatusName> allowedStatuses = [new JiraStatusName("Done")];

        // Act
        var result = statusStatistics.BuildTopDisallowedStatusLabels(allowedStatuses, 2);

        // Assert
        result.Should().Equal("Blocked (7)", "Review (7)");
    }

    [Fact(DisplayName = "PresentationHelpers BuildTopDisallowedStatusLabels throws for invalid max items.")]
    [Trait("Category", "Unit")]
    public void BuildTopDisallowedStatusLabelsThrowsForInvalidMaxItems()
    {
        // Arrange
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics = new Dictionary<JiraStatusName, int>();
        IReadOnlyCollection<JiraStatusName> allowedStatuses = [];

        // Act
        Action action = () => _ = statusStatistics.BuildTopDisallowedStatusLabels(allowedStatuses, 0);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxItems");
    }

    [Fact(DisplayName = "PresentationHelpers FilterRowsByAllowedJiraStatuses returns all rows when allowed statuses are empty.")]
    [Trait("Category", "Unit")]
    public void FilterRowsByAllowedJiraStatusesReturnsAllRowsWhenAllowedStatusesAreEmpty()
    {
        // Arrange
        IReadOnlyList<ComponentCheckRow> rows =
        [
            CreateRow(1, CheckStatus.Outdated, [CreateVersion("2.0.0", "APP-1", "Done")]),
            CreateRow(2, CheckStatus.UpToDate, []),
        ];
        var allowedStatuses = Array.Empty<JiraStatusName>();

        // Act
        var result = rows.FilterRowsByAllowedJiraStatuses(allowedStatuses);

        // Assert
        result.Should().Equal(rows);
    }

    [Fact(DisplayName = "PresentationHelpers FilterRowsByAllowedJiraStatuses keeps rows where all task statuses are allowed.")]
    [Trait("Category", "Unit")]
    public void FilterRowsByAllowedJiraStatusesKeepsRowsWhereAllTaskStatusesAreAllowed()
    {
        // Arrange
        IReadOnlyList<ComponentCheckRow> rows =
        [
            CreateRow(1, CheckStatus.Outdated, [CreateVersion("2.0.0", "APP-1", "Done")]),
            CreateRow(2, CheckStatus.Outdated, [CreateVersion("2.0.0", "APP-2", "Done, In Progress")]),
            CreateRow(3, CheckStatus.Outdated, []),
        ];
        var allowedStatuses = new[] { new JiraStatusName("Done") };

        // Act
        var result = rows.FilterRowsByAllowedJiraStatuses(allowedStatuses);

        // Assert
        result.Should().HaveCount(1);
        result[0].Index.Should().Be(new ComponentCheckIndex(1));
    }

    [Fact(DisplayName = "PresentationHelpers HasDetails returns expected values.")]
    [Trait("Category", "Unit")]
    public void HasDetailsReturnsExpectedValues()
    {
        // Arrange
        string? nullValue = null;
        var whitespaceValue = "   ";
        var dashValue = " - ";
        var textValue = "Needs action";

        // Act
        var nullResult = nullValue.HasDetails();
        var whitespaceResult = whitespaceValue.HasDetails();
        var dashResult = dashValue.HasDetails();
        var textResult = textValue.HasDetails();

        // Assert
        nullResult.Should().BeFalse();
        whitespaceResult.Should().BeFalse();
        dashResult.Should().BeFalse();
        textResult.Should().BeTrue();
    }

    [Fact(DisplayName = "PresentationHelpers BuildTaskAlertDetailsByTask merges task details and sorts by task key.")]
    [Trait("Category", "Unit")]
    public void BuildTaskAlertDetailsByTaskMergesTaskDetailsAndSortsByTaskKey()
    {
        // Arrange
        IReadOnlyList<VersionJiraRow> versions =
        [
            new VersionJiraRow(
                new VersionLabel("2.0.0"),
                new JiraTaskReference("APP-1"),
                new JiraTitleReference("Title"),
                new JiraStatusReference("Done"),
                [
                    new JiraTaskAlertDetails(
                        new JiraTaskReference("APP-2"),
                        new JiraTitleReference("Second"),
                        new JiraStatusReference("Done"),
                        "Second required",
                        null),
                    new JiraTaskAlertDetails(
                        new JiraTaskReference("APP-1"),
                        new JiraTitleReference("N/A"),
                        new JiraStatusReference("N/A"),
                        null,
                        "Initial breaking"),
                ],
                hasRequiredActions: true,
                hasBreakingChanges: true,
                hasDependencyIssues: false),
            new VersionJiraRow(
                new VersionLabel("2.1.0"),
                new JiraTaskReference("APP-1"),
                new JiraTitleReference("Title"),
                new JiraStatusReference("Done"),
                [
                    new JiraTaskAlertDetails(
                        new JiraTaskReference("APP-1"),
                        new JiraTitleReference("Resolved title"),
                        new JiraStatusReference("In Progress"),
                        "New required",
                        null),
                ],
                hasRequiredActions: true,
                hasBreakingChanges: false,
                hasDependencyIssues: false),
        ];

        // Act
        var result = versions.BuildTaskAlertDetailsByTask();

        // Assert
        result.Should().HaveCount(2);
        result[0].Task.Should().Be(new JiraTaskReference("APP-1"));
        result[0].Title.Should().Be(new JiraTitleReference("Resolved title"));
        result[0].Status.Should().Be(new JiraStatusReference("In Progress"));
        result[0].RequiredActionsDetails.Should().Be("New required");
        result[0].BreakingChangesDetails.Should().Be("Initial breaking");

        result[1].Task.Should().Be(new JiraTaskReference("APP-2"));
        result[1].RequiredActionsDetails.Should().Be("Second required");
    }

    [Fact(DisplayName = "PresentationHelpers BuildUniqueJiraTaskCountsByStatus counts unique valid Jira tasks.")]
    [Trait("Category", "Unit")]
    public void BuildUniqueJiraTaskCountsByStatusCountsUniqueValidJiraTasks()
    {
        // Arrange
        IReadOnlyList<ComponentCheckRow> rows =
        [
            CreateRow(
                1,
                CheckStatus.Outdated,
                [
                    CreateVersion("2.0.0", "APP-1, APP-2, N/A, BAD, APP-3", "Done, In Progress, Done, Done"),
                    CreateVersion("2.0.1", "APP-1", "Done"),
                    CreateVersion("2.0.2", "OPS_1-9, INVALID-ABC", "Review"),
                ]),
        ];

        // Act
        var result = rows.BuildUniqueJiraTaskCountsByStatus();

        // Assert
        result.Should().HaveCount(3);
        result[new JiraStatusName("Done")].Should().Be(2);
        result[new JiraStatusName("In Progress")].Should().Be(1);
        result[new JiraStatusName("Review")].Should().Be(1);
    }

    [Fact(DisplayName = "PresentationHelpers ToPlainLabel maps known statuses and falls back for unknown values.")]
    [Trait("Category", "Unit")]
    public void ToPlainLabelMapsKnownStatusesAndFallsBackForUnknownValues()
    {
        // Arrange
        var unknownStatus = (CheckStatus)999;

        // Act
        var upToDate = CheckStatus.UpToDate.ToPlainLabel();
        var outdated = CheckStatus.Outdated.ToPlainLabel();
        var notFound = CheckStatus.RepositoryNotFound.ToPlainLabel();
        var bitbucketError = CheckStatus.BitbucketError.ToPlainLabel();
        var invalidVersion = CheckStatus.InvalidCurrentVersion.ToPlainLabel();
        var unknown = unknownStatus.ToPlainLabel();

        // Assert
        upToDate.Should().Be("Up to date");
        outdated.Should().Be("Outdated");
        notFound.Should().Be("Repo not found");
        bitbucketError.Should().Be("Bitbucket error");
        invalidVersion.Should().Be("Invalid version");
        unknown.Should().Be("Unknown");
    }

    [Fact(DisplayName = "PresentationHelpers ToAheadReleasesLabel formats singular and plural counters.")]
    [Trait("Category", "Unit")]
    public void ToAheadReleasesLabelFormatsSingularAndPluralCounters()
    {
        // Arrange
        var singularCount = 1;
        var pluralCount = 4;

        // Act
        var singular = singularCount.ToAheadReleasesLabel();
        var plural = pluralCount.ToAheadReleasesLabel();

        // Assert
        singular.Should().Be("1 release ahead");
        plural.Should().Be("4 releases ahead");
    }

    private static ComponentCheckRow CreateRow(
        int index,
        CheckStatus status,
        IReadOnlyList<VersionJiraRow> newerVersions) =>
        new(
            new ComponentCheckIndex(index),
            new ComponentName($"component-{index}"),
            new RepositoryName($"repo-{index}"),
            new VersionLabel("1.0.0"),
            status,
            RowDetails.CreatePlaceholder(),
            newerVersions);

    private static VersionJiraRow CreateVersion(string version, string jiraTask, string jiraStatus) =>
        new(
            new VersionLabel(version),
            new JiraTaskReference(jiraTask),
            new JiraTitleReference("Task title"),
            new JiraStatusReference(jiraStatus),
            [
                new JiraTaskAlertDetails(
                    new JiraTaskReference("APP-1"),
                    new JiraTitleReference("Task title"),
                    new JiraStatusReference("Done"),
                    null,
                    null),
            ],
            hasRequiredActions: false,
            hasBreakingChanges: false,
            hasDependencyIssues: false);
}
