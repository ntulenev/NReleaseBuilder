using FluentAssertions;

using NReleaseBuilder.Presentation;

namespace NReleaseBuilder.Tests.Presentation;

public class JiraTaskParsingHelpersTests
{
    [Fact(DisplayName = "JiraTaskParsingHelpers SplitTaskValues splits, trims, and ignores empty values.")]
    [Trait("Category", "Unit")]
    public void SplitTaskValuesSplitsTrimsAndIgnoresEmptyValues()
    {
        // Arrange
        var value = " APP-1, , APP-2 ,, OPS_1-9 ";

        // Act
        var result = JiraTaskParsingHelpers.SplitTaskValues(value);

        // Assert
        result.Should().Equal("APP-1", "APP-2", "OPS_1-9");
    }

    [Fact(DisplayName = "JiraTaskParsingHelpers SplitTaskValues returns empty array for whitespace input.")]
    [Trait("Category", "Unit")]
    public void SplitTaskValuesReturnsEmptyArrayForWhitespaceInput()
    {
        // Arrange
        var value = "   ";

        // Act
        var result = JiraTaskParsingHelpers.SplitTaskValues(value);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory(DisplayName = "JiraTaskParsingHelpers IsTrackableJiraTask returns true for valid task keys.")]
    [Trait("Category", "Unit")]
    [InlineData("APP-1")]
    [InlineData("OPS_1-9")]
    [InlineData("A1B-123")]
    public void IsTrackableJiraTaskReturnsTrueForValidTaskKeys(string taskKey)
    {
        // Arrange
        // Act
        var result = JiraTaskParsingHelpers.IsTrackableJiraTask(taskKey);

        // Assert
        result.Should().BeTrue();
    }

    [Theory(DisplayName = "JiraTaskParsingHelpers IsTrackableJiraTask returns false for invalid task keys.")]
    [Trait("Category", "Unit")]
    [InlineData("N/A")]
    [InlineData("APP")]
    [InlineData("-1")]
    [InlineData("1APP-1")]
    [InlineData("APP-")]
    [InlineData("APP-ABC")]
    [InlineData("APP-3a")]
    [InlineData("APP.$-1")]
    public void IsTrackableJiraTaskReturnsFalseForInvalidTaskKeys(string taskKey)
    {
        // Arrange
        // Act
        var result = JiraTaskParsingHelpers.IsTrackableJiraTask(taskKey);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "JiraTaskParsingHelpers IsTrackableJiraTask throws for null task key.")]
    [Trait("Category", "Unit")]
    public void IsTrackableJiraTaskThrowsForNullTaskKey()
    {
        // Arrange
        // Act
        Action action = () => _ = JiraTaskParsingHelpers.IsTrackableJiraTask(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("taskKey");
    }

    [Fact(DisplayName = "JiraTaskParsingHelpers MatchJiraBrowseUrls finds Jira browse URLs and task captures.")]
    [Trait("Category", "Unit")]
    public void MatchJiraBrowseUrlsFindsJiraBrowseUrlsAndTaskCaptures()
    {
        // Arrange
        var value = "See https://jira.example.test/browse/APP-42 and (https://jira.example.test/browse/OPS_1-9).";

        // Act
        var matches = JiraTaskParsingHelpers.MatchJiraBrowseUrls(value);

        // Assert
        matches.Should().HaveCount(2);
        matches[0].Groups["task"].Value.Should().Be("APP-42");
        matches[1].Groups["task"].Value.Should().Be("OPS_1-9");
    }

    [Fact(DisplayName = "JiraTaskParsingHelpers MatchJiraTaskKeys finds only standalone valid task keys.")]
    [Trait("Category", "Unit")]
    public void MatchJiraTaskKeysFindsOnlyStandaloneValidTaskKeys()
    {
        // Arrange
        var value = "Refs: APP-1,OPS_1-9,_APP-2, APP-XYZ, APP-3a, [ADF-13848].";

        // Act
        var matches = JiraTaskParsingHelpers.MatchJiraTaskKeys(value);
        var taskValues = matches.Select(match => match.Groups["task"].Value).ToArray();

        // Assert
        taskValues.Should().Equal("APP-1", "OPS_1-9", "ADF-13848");
    }

    [Fact(DisplayName = "JiraTaskParsingHelpers MatchJiraBrowseUrls throws for null value.")]
    [Trait("Category", "Unit")]
    public void MatchJiraBrowseUrlsThrowsForNullValue()
    {
        // Arrange
        // Act
        Action action = () => _ = JiraTaskParsingHelpers.MatchJiraBrowseUrls(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("value");
    }

    [Fact(DisplayName = "JiraTaskParsingHelpers MatchJiraTaskKeys throws for null value.")]
    [Trait("Category", "Unit")]
    public void MatchJiraTaskKeysThrowsForNullValue()
    {
        // Arrange
        // Act
        Action action = () => _ = JiraTaskParsingHelpers.MatchJiraTaskKeys(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("value");
    }
}
