using FluentAssertions;

using NReleaseBuilder.Jira;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Jira;

public class JiraStatusStatisticsConverterTests
{
    [Fact(DisplayName = "JiraStatusStatisticsConverter Convert throws when rows are null.")]
    [Trait("Category", "Unit")]
    public void ConvertThrowsWhenRowsAreNull()
    {
        // Arrange
        var sut = new JiraStatusStatisticsConverter();

        // Act
        Action action = () => _ = sut.Convert(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("rows");
    }

    [Fact(DisplayName = "JiraStatusStatisticsConverter Convert returns empty dictionary for rows without newer versions.")]
    [Trait("Category", "Unit")]
    public void ConvertReturnsEmptyDictionaryForRowsWithoutNewerVersions()
    {
        // Arrange
        var sut = new JiraStatusStatisticsConverter();
        IReadOnlyList<ComponentCheckRow> rows =
        [
            CreateRow(1, []),
            CreateRow(2, []),
        ];

        // Act
        var result = sut.Convert(rows);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "JiraStatusStatisticsConverter Convert aggregates split statuses across rows.")]
    [Trait("Category", "Unit")]
    public void ConvertAggregatesSplitStatusesAcrossRows()
    {
        // Arrange
        var sut = new JiraStatusStatisticsConverter();
        IReadOnlyList<ComponentCheckRow> rows =
        [
            CreateRow(1,
            [
                CreateVersionRow("1.1.0", "Done, In Progress"),
                CreateVersionRow("1.2.0", "Done"),
            ]),
            CreateRow(2,
            [
                CreateVersionRow("2.0.0", "In Progress, Blocked"),
            ]),
        ];

        // Act
        var result = sut.Convert(rows);

        // Assert
        result.Should().HaveCount(3);
        result[new JiraStatusName("Done")].Should().Be(2);
        result[new JiraStatusName("In Progress")].Should().Be(2);
        result[new JiraStatusName("Blocked")].Should().Be(1);
    }

    private static ComponentCheckRow CreateRow(int index, IReadOnlyList<VersionJiraRow> newerVersions) =>
        new(
            new ComponentCheckIndex(index),
            new ComponentName($"component-{index}"),
            new RepositoryName($"repo-{index}"),
            new VersionLabel("1.0.0"),
            CheckStatus.Outdated,
            RowDetails.CreatePlaceholder(),
            newerVersions);

    private static VersionJiraRow CreateVersionRow(string version, string status) =>
        new(
            new VersionLabel(version),
            new JiraTaskReference("PROJ-1"),
            new JiraTitleReference("Issue title"),
            new JiraStatusReference(status),
            [],
            hasRequiredActions: false,
            hasBreakingChanges: false,
            hasDependencyIssues: false);
}
