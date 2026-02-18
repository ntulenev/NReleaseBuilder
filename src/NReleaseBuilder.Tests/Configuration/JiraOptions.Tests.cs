using FluentAssertions;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Configuration;

public class JiraOptionsTests
{
    [Fact(DisplayName = "JiraOptions ResolveAuthEmail prefers Email and trims value.")]
    [Trait("Category", "Unit")]
    public void ResolveAuthEmailPrefersEmailAndTrimsValue()
    {
        // Arrange
        var options = CreateOptions(
            email: "  jira@example.test  ",
            authEmail: "alias@example.test");

        // Act
        var result = options.ResolveAuthEmail();

        // Assert
        result.Should().Be("jira@example.test");
    }

    [Fact(DisplayName = "JiraOptions ResolveAuthEmail falls back to AuthEmail alias.")]
    [Trait("Category", "Unit")]
    public void ResolveAuthEmailFallsBackToAuthEmailAlias()
    {
        // Arrange
        var options = CreateOptions(
            email: " ",
            authEmail: "  alias@example.test  ");

        // Act
        var result = options.ResolveAuthEmail();

        // Assert
        result.Should().Be("alias@example.test");
    }

    [Fact(DisplayName = "JiraOptions ResolveAuthApiToken prefers ApiToken and trims value.")]
    [Trait("Category", "Unit")]
    public void ResolveAuthApiTokenPrefersApiTokenAndTrimsValue()
    {
        // Arrange
        var options = CreateOptions(
            apiToken: "  token-123  ",
            authApiToken: "alias-token");

        // Act
        var result = options.ResolveAuthApiToken();

        // Assert
        result.Should().Be("token-123");
    }

    [Fact(DisplayName = "JiraOptions ResolveAuthApiToken falls back to AuthApiToken alias.")]
    [Trait("Category", "Unit")]
    public void ResolveAuthApiTokenFallsBackToAuthApiTokenAlias()
    {
        // Arrange
        var options = CreateOptions(
            apiToken: " ",
            authApiToken: "  alias-token  ");

        // Act
        var result = options.ResolveAuthApiToken();

        // Assert
        result.Should().Be("alias-token");
    }

    [Fact(DisplayName = "JiraOptions BuildAllowedStatuses returns distinct statuses.")]
    [Trait("Category", "Unit")]
    public void BuildAllowedStatusesReturnsDistinctStatuses()
    {
        // Arrange
        var options = CreateOptions(
            allowedTaskStatuses: ["Done", "done", "In Progress"]);

        // Act
        var result = options.BuildAllowedStatuses();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(new JiraStatusName("Done"));
        result.Should().Contain(new JiraStatusName("In Progress"));
    }

    [Fact(DisplayName = "JiraOptions BuildAllowedStatuses throws when allowed statuses are null.")]
    [Trait("Category", "Unit")]
    public void BuildAllowedStatusesThrowsWhenAllowedStatusesAreNull()
    {
        // Arrange
        var options = new JiraOptions
        {
            BaseUrl = new Uri("https://jira.example.test/"),
            Email = "jira@example.test",
            ApiToken = "token",
            AllowedTaskStatuses = null!,
        };

        // Act
        Action action = () => _ = options.BuildAllowedStatuses();

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("AllowedTaskStatuses");
    }

    private static JiraOptions CreateOptions(
        string? email = "jira@example.test",
        string? authEmail = "",
        string? apiToken = "token",
        string? authApiToken = "",
        IReadOnlyList<string>? allowedTaskStatuses = null) =>
        new()
        {
            BaseUrl = new Uri("https://jira.example.test/"),
            Email = email ?? string.Empty,
            AuthEmail = authEmail ?? string.Empty,
            ApiToken = apiToken ?? string.Empty,
            AuthApiToken = authApiToken ?? string.Empty,
            AllowedTaskStatuses = allowedTaskStatuses ?? ["Done"],
        };
}
