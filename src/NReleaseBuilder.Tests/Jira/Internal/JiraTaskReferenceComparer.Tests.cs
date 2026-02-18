using System.Reflection;

using FluentAssertions;

using NReleaseBuilder.Jira.Internal;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Jira.Internal;

public class JiraTaskReferenceComparerTests
{
    [Fact(DisplayName = "JiraTaskReferenceComparer exposes singleton instance.")]
    [Trait("Category", "Unit")]
    public void ExposesSingletonInstance()
    {
        // Arrange
        var comparerType = ResolveComparerType();
        var instanceProperty = comparerType.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected public static Instance property.");

        // Act
        var first = instanceProperty.GetValue(null);
        var second = instanceProperty.GetValue(null);

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first.Should().BeSameAs(second);
        first.Should().BeAssignableTo<IEqualityComparer<JiraTaskReference>>();
    }

    [Fact(DisplayName = "JiraTaskReferenceComparer compares task references case-insensitively.")]
    [Trait("Category", "Unit")]
    public void ComparesTaskReferencesCaseInsensitively()
    {
        // Arrange
        var comparer = ResolveComparer();
        var left = new JiraTaskReference("PROJ-1");
        var right = new JiraTaskReference("proj-1");
        var other = new JiraTaskReference("PROJ-2");

        // Act
        var equal = comparer.Equals(left, right);
        var notEqual = comparer.Equals(left, other);

        // Assert
        equal.Should().BeTrue();
        notEqual.Should().BeFalse();
    }

    [Fact(DisplayName = "JiraTaskReferenceComparer returns stable case-insensitive hash code.")]
    [Trait("Category", "Unit")]
    public void ReturnsStableCaseInsensitiveHashCode()
    {
        // Arrange
        var comparer = ResolveComparer();
        var upper = new JiraTaskReference("PROJ-1");
        var lower = new JiraTaskReference("proj-1");

        // Act
        var upperHash = comparer.GetHashCode(upper);
        var lowerHash = comparer.GetHashCode(lower);

        // Assert
        upperHash.Should().Be(lowerHash);
    }

    private static Type ResolveComparerType()
    {
        return typeof(JiraParser).Assembly.GetType(
            "NReleaseBuilder.Jira.Internal.JiraTaskReferenceComparer",
            throwOnError: true)
            ?? throw new InvalidOperationException("Comparer type was not found.");
    }

    private static IEqualityComparer<JiraTaskReference> ResolveComparer()
    {
        var comparerType = ResolveComparerType();
        var instanceProperty = comparerType.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected public static Instance property.");

        var instance = instanceProperty.GetValue(null)
            ?? throw new InvalidOperationException("Instance property returned null.");

        return (IEqualityComparer<JiraTaskReference>)instance;
    }
}
