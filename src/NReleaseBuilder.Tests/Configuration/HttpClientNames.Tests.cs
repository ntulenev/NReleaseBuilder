using FluentAssertions;

using NReleaseBuilder.Configuration;

namespace NReleaseBuilder.Tests.Configuration;

public class HttpClientNamesTests
{
    [Fact(DisplayName = "HttpClientNames exposes expected constant values.")]
    [Trait("Category", "Unit")]
    public void ExposesExpectedConstantValues()
    {
        // Arrange
        // Act
        var bitbucket = HttpClientNames.BITBUCKET;
        var jira = HttpClientNames.JIRA;

        // Assert
        bitbucket.Should().Be("Bitbucket");
        jira.Should().Be("Jira");
    }
}
