using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Components;

public class ComponentCheckRowTests
{
    [Fact(DisplayName = "ComponentCheckRow throws when newer versions are null.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenNewerVersionsAreNull()
    {
        // Arrange
        // Act
        Action action = () => _ = new ComponentCheckRow(
            new ComponentCheckIndex(1),
            new ComponentName("component"),
            new RepositoryName("repo"),
            new VersionLabel("1.0.0"),
            CheckStatus.UpToDate,
            RowDetails.CreatePlaceholder(),
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("newerVersions");
    }

    [Fact(DisplayName = "ComponentCheckRow throws when status value is invalid.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenStatusValueIsInvalid()
    {
        // Arrange
        var invalidStatus = (CheckStatus)999;

        // Act
        Action action = () => _ = new ComponentCheckRow(
            new ComponentCheckIndex(1),
            new ComponentName("component"),
            new RepositoryName("repo"),
            new VersionLabel("1.0.0"),
            invalidStatus,
            RowDetails.CreatePlaceholder(),
            []);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("status");
    }

    [Fact(DisplayName = "ComponentCheckRow stores values.")]
    [Trait("Category", "Unit")]
    public void StoresValues()
    {
        // Arrange
        IReadOnlyList<VersionJiraRow> newerVersions =
        [
            Version("1.1.0", "PROJ-1", "Done"),
        ];
        var row = new ComponentCheckRow(
            new ComponentCheckIndex(2),
            new ComponentName("component"),
            new RepositoryName("repo"),
            new VersionLabel("1.0.0"),
            CheckStatus.Outdated,
            new RowDetails("details"),
            newerVersions);

        // Act
        var index = row.Index.Value;
        var status = row.Status;
        var details = row.DetailsMessage.Value;

        // Assert
        index.Should().Be(2);
        status.Should().Be(CheckStatus.Outdated);
        details.Should().Be("details");
        row.NewerVersions.Should().BeSameAs(newerVersions);
    }

    [Fact(DisplayName = "ComponentCheckRow MatchesStatusFilter throws when allowed statuses are null.")]
    [Trait("Category", "Unit")]
    public void MatchesStatusFilterThrowsWhenAllowedStatusesAreNull()
    {
        // Arrange
        var row = CreateRow([]);

        // Act
        Action action = () => _ = row.MatchesStatusFilter(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("allowedStatuses");
    }

    [Fact(DisplayName = "ComponentCheckRow MatchesStatusFilter returns false when there are no task statuses.")]
    [Trait("Category", "Unit")]
    public void MatchesStatusFilterReturnsFalseWhenThereAreNoTaskStatuses()
    {
        // Arrange
        var row = CreateRow([]);
        var allowed = new HashSet<JiraStatusName> { new("Done") };

        // Act
        var matches = row.MatchesStatusFilter(allowed);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName = "ComponentCheckRow MatchesStatusFilter returns false when any status is not allowed.")]
    [Trait("Category", "Unit")]
    public void MatchesStatusFilterReturnsFalseWhenAnyStatusIsNotAllowed()
    {
        // Arrange
        var row = CreateRow(
        [
            Version("1.1.0", "PROJ-1", "Done, In Progress"),
        ]);
        var allowed = new HashSet<JiraStatusName> { new("Done") };

        // Act
        var matches = row.MatchesStatusFilter(allowed);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName = "ComponentCheckRow MatchesStatusFilter returns true when all statuses are allowed.")]
    [Trait("Category", "Unit")]
    public void MatchesStatusFilterReturnsTrueWhenAllStatusesAreAllowed()
    {
        // Arrange
        var row = CreateRow(
        [
            Version("1.1.0", "PROJ-1", "Done, In Progress"),
            Version("1.2.0", "PROJ-2", "In Progress"),
        ]);
        var allowed = new HashSet<JiraStatusName>
        {
            new("Done"),
            new("In Progress"),
        };

        // Act
        var matches = row.MatchesStatusFilter(allowed);

        // Assert
        matches.Should().BeTrue();
    }

    private static ComponentCheckRow CreateRow(IReadOnlyList<VersionJiraRow> newerVersions) =>
        new(
            new ComponentCheckIndex(1),
            new ComponentName("component"),
            new RepositoryName("repo"),
            new VersionLabel("1.0.0"),
            CheckStatus.Outdated,
            RowDetails.CreatePlaceholder(),
            newerVersions);

    private static VersionJiraRow Version(string version, string task, string status) =>
        new(
            new VersionLabel(version),
            new JiraTaskReference(task),
            new JiraTitleReference("Issue"),
            new JiraStatusReference(status),
            [],
            false,
            false,
            false);
}
