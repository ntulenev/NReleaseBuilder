using FluentAssertions;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Tests.Models.Components;

public class ComponentSourceSnapshotTests
{
    [Fact(DisplayName = "ComponentSourceSnapshot builds difference rows across dev target and settings.")]
    [Trait("Category", "Unit")]
    public void BuildComponentSourceDifferenceRowsBuildsDifferenceRowsAcrossDevTargetAndSettings()
    {
        // Arrange
        var snapshot = new ComponentSourceSnapshot(
        [
            new ComponentName("service1"),
        ],
        [
            new ComponentName("service3"),
        ]);

        var settings = CreateSettings(csvComponentGroups:
        [
            new CsvComponentGroupOptions
            {
                Name = "Group",
                ComponentNames = ["service1", "service2"],
            },
        ]);

        // Act
        var rows = snapshot.BuildComponentSourceDifferenceRows(settings);

        // Assert
        rows.Should().Equal(
            new ComponentSourceDifferenceRow(new ComponentName("service1"), true, false, true),
            new ComponentSourceDifferenceRow(new ComponentName("service2"), false, false, true),
            new ComponentSourceDifferenceRow(new ComponentName("service3"), false, true, false));
    }

    [Fact(DisplayName = "ComponentSourceSnapshot returns no difference rows when sources and settings match.")]
    [Trait("Category", "Unit")]
    public void BuildComponentSourceDifferenceRowsReturnsNoDifferenceRowsWhenSourcesAndSettingsMatch()
    {
        // Arrange
        var snapshot = new ComponentSourceSnapshot(
        [
            new ComponentName("service1"),
            new ComponentName("service2"),
        ],
        [
            new ComponentName("service1"),
            new ComponentName("service2"),
        ]);

        var settings = CreateSettings(csvComponentNamesFilter: ["service1", "service2"]);

        // Act
        var rows = snapshot.BuildComponentSourceDifferenceRows(settings);

        // Assert
        rows.Should().BeEmpty();
    }

    private static AppSettings CreateSettings(
        IReadOnlyList<string>? csvComponentNamesFilter = null,
        IReadOnlyList<CsvComponentGroupOptions>? csvComponentGroups = null) =>
        new()
        {
            DevCsvFilePath = "dev-components.csv",
            TargetCsvFilePath = "target-components.csv",
            CsvComponentNamesFilter = csvComponentNamesFilter ?? [],
            CsvComponentGroups = csvComponentGroups ?? [],
            Bitbucket = new BitbucketOptions
            {
                BaseUrl = new Uri("https://bitbucket.example.test/"),
                Workspace = "workspace",
                ProjectNames = ["PROJ"],
                AuthEmail = "bot@example.test",
                AuthApiToken = "token",
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
                Email = "jira@example.test",
                ApiToken = "token",
            },
        };
}
