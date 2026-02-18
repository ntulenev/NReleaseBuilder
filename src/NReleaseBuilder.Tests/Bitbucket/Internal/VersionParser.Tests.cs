using FluentAssertions;

using NReleaseBuilder.Bitbucket.Internal;
using NReleaseBuilder.Models.Bitbucket;

using NuGet.Versioning;

namespace NReleaseBuilder.Tests.Bitbucket.Internal;

public class VersionParserTests
{
    [Fact(DisplayName = "VersionParser TryParse parses plain semantic version.")]
    [Trait("Category", "Unit")]
    public void TryParseParsesPlainSemanticVersion()
    {
        // Arrange
        var input = "1.2.3";

        // Act
        var success = VersionParser.TryParse(input, out var parsedVersion);

        // Assert
        success.Should().BeTrue();
        parsedVersion.Should().Be(NuGetVersion.Parse("1.2.3"));
    }

    [Fact(DisplayName = "VersionParser TryParse parses version label with v prefix.")]
    [Trait("Category", "Unit")]
    public void TryParseParsesVersionLabelWithVPrefix()
    {
        // Arrange
        var input = new VersionLabel("v2.0.1");

        // Act
        var success = VersionParser.TryParse(input, out var parsedVersion);

        // Assert
        success.Should().BeTrue();
        parsedVersion.Should().Be(NuGetVersion.Parse("2.0.1"));
    }

    [Fact(DisplayName = "VersionParser TryParse extracts semantic version from free-form text.")]
    [Trait("Category", "Unit")]
    public void TryParseExtractsSemanticVersionFromFreeFormText()
    {
        // Arrange
        var input = "release-2024.10.5+build";

        // Act
        var success = VersionParser.TryParse(input, out var parsedVersion);

        // Assert
        success.Should().BeTrue();
        parsedVersion.Should().Be(NuGetVersion.Parse("2024.10.5+build"));
    }

    [Fact(DisplayName = "VersionParser TryParse returns false for invalid input.")]
    [Trait("Category", "Unit")]
    public void TryParseReturnsFalseForInvalidInput()
    {
        // Arrange
        var input = "not-a-version";

        // Act
        var success = VersionParser.TryParse(input, out var parsedVersion);

        // Assert
        success.Should().BeFalse();
        parsedVersion.Should().Be(NuGetVersion.Parse("0.0.0"));
    }
}
