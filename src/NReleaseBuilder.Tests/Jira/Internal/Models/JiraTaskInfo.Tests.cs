using System.Net;

using FluentAssertions;

using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Jira.Internal.Models;

public class JiraTaskInfoTests
{
    [Fact(DisplayName = "JiraTaskInfo FromIssueInfo maps issue values.")]
    [Trait("Category", "Unit")]
    public void FromIssueInfoMapsIssueValues()
    {
        // Arrange
        var issue = new JiraIssueInfo(
            new JiraStatusName("Done"),
            "Task title",
            "Required actions",
            "Breaking changes");

        // Act
        var result = JiraTaskInfo.FromIssueInfo(issue);

        // Assert
        result.Status.Should().Be("Done");
        result.Title.Should().Be("Task title");
        result.RequiredActionsDetails.Should().Be("Required actions");
        result.BreakingChangesDetails.Should().Be("Breaking changes");
        result.HasRequiredActions.Should().BeTrue();
        result.HasBreakingChanges.Should().BeTrue();
    }

    [Fact(DisplayName = "JiraTaskInfo FromIssueInfo returns fallback values for null issue.")]
    [Trait("Category", "Unit")]
    public void FromIssueInfoReturnsFallbackValuesForNullIssue()
    {
        // Arrange
        // Act
        var result = JiraTaskInfo.FromIssueInfo(null);

        // Assert
        result.Status.Should().Be("N/A");
        result.Title.Should().Be("N/A");
        result.RequiredActionsDetails.Should().BeNull();
        result.BreakingChangesDetails.Should().BeNull();
        result.HasRequiredActions.Should().BeFalse();
        result.HasBreakingChanges.Should().BeFalse();
    }

    [Fact(DisplayName = "JiraTaskInfo NotFound uses provided status when available.")]
    [Trait("Category", "Unit")]
    public void NotFoundUsesProvidedStatusWhenAvailable()
    {
        // Arrange
        // Act
        var result = JiraTaskInfo.NotFound("In Progress");

        // Assert
        result.Status.Should().Be("In Progress");
        result.Title.Should().Be("N/A");
    }

    [Fact(DisplayName = "JiraTaskInfo NotFound defaults status when search status is missing.")]
    [Trait("Category", "Unit")]
    public void NotFoundDefaultsStatusWhenSearchStatusIsMissing()
    {
        // Arrange
        // Act
        var result = JiraTaskInfo.NotFound(null);

        // Assert
        result.Status.Should().Be("Not found");
        result.Title.Should().Be("N/A");
    }

    [Fact(DisplayName = "JiraTaskInfo HttpError maps status code.")]
    [Trait("Category", "Unit")]
    public void HttpErrorMapsStatusCode()
    {
        // Arrange
        // Act
        var result = JiraTaskInfo.HttpError(HttpStatusCode.BadGateway);

        // Assert
        result.Status.Should().Be("HTTP 502");
        result.Title.Should().Be("N/A");
    }

    [Fact(DisplayName = "JiraTaskInfo IsTransientStatus returns true for transient http statuses.")]
    [Trait("Category", "Unit")]
    public void IsTransientStatusReturnsTrueForTransientHttpStatuses()
    {
        // Arrange
        var transientStatuses = new[]
        {
            "HTTP 408",
            "HTTP 429",
            "HTTP 500",
            "HTTP 502",
            "HTTP 503",
            "HTTP 504",
        };

        // Act
        var results = transientStatuses
            .Select(status => new JiraTaskInfo(status, "N/A", null, null).IsTransientStatus())
            .ToArray();

        // Assert
        results.Should().OnlyContain(value => value);
    }

    [Fact(DisplayName = "JiraTaskInfo IsTransientStatus returns false for non transient statuses.")]
    [Trait("Category", "Unit")]
    public void IsTransientStatusReturnsFalseForNonTransientStatuses()
    {
        // Arrange
        var notTransient = new JiraTaskInfo("HTTP 404", "N/A", null, null);

        // Act
        var result = notTransient.IsTransientStatus();

        // Assert
        result.Should().BeFalse();
    }
}
