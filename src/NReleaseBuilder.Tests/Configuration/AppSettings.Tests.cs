using FluentAssertions;

using NReleaseBuilder.Configuration;

namespace NReleaseBuilder.Tests.Configuration;

public class AppSettingsTests
{
    [Fact(DisplayName = "AppSettings initializes defaults and stores required values.")]
    [Trait("Category", "Unit")]
    public void InitializesDefaultsAndStoresRequiredValues()
    {
        // Arrange
        var settings = new AppSettings
        {
            CsvFilePath = "components.csv",
            Bitbucket = CreateBitbucketOptions(),
            Jira = CreateJiraOptions(),
        };

        // Act
        var csvPath = settings.CsvFilePath;
        var filter = settings.CsvComponentNamesFilter;
        var pdf = settings.Pdf;

        // Assert
        csvPath.Should().Be("components.csv");
        filter.Should().NotBeNull();
        filter.Should().BeEmpty();
        settings.Bitbucket.Should().NotBeNull();
        settings.Jira.Should().NotBeNull();
        pdf.Should().NotBeNull();
    }

    private static BitbucketOptions CreateBitbucketOptions() =>
        new()
        {
            BaseUrl = new Uri("https://bitbucket.example.test/"),
            Workspace = "workspace",
            ProjectNames = ["PROJ"],
            AuthEmail = "bot@example.test",
            AuthApiToken = "token",
        };

    private static JiraOptions CreateJiraOptions() =>
        new()
        {
            BaseUrl = new Uri("https://jira.example.test/"),
            Email = "jira@example.test",
            ApiToken = "token",
        };
}
