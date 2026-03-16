using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Tests.Models.Components;

public class ComponentRowTests
{
    [Fact(DisplayName = "ComponentRow stores values.")]
    [Trait("Category", "Unit")]
    public void StoresValues()
    {
        // Arrange
        var row = new ComponentRow(
            new ComponentName("component"),
            new RepositoryName("repo"),
            new VersionLabel("1.0.0"));

        // Act
        var component = row.Component.Value;
        var repository = row.Repository.Value;
        var version = row.Version.Value;
        var isReleased = row.IsReleased;

        // Assert
        component.Should().Be("component");
        repository.Should().Be("repo");
        version.Should().Be("1.0.0");
        isReleased.Should().BeTrue();
    }
}
