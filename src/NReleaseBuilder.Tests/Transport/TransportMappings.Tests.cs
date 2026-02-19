using System.Text.Json;

using FluentAssertions;

using NReleaseBuilder.Transport;
using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport;

public class TransportMappingsTests
{
    [Fact(DisplayName = "TransportMappings ToDomain for TagPageDto throws when dto is null.")]
    [Trait("Category", "Unit")]
    public void ToDomainTagPageThrowsWhenDtoIsNull()
    {
        // Arrange
        TagPageDto dto = null!;

        // Act
        Action action = () => _ = dto.ToDomain();

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("dto");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for TagPageDto maps tags and next uri.")]
    [Trait("Category", "Unit")]
    public void ToDomainTagPageMapsTagsAndNextUri()
    {
        // Arrange
        IReadOnlyList<TagDto> values =
        [
            null!,
            new TagDto
            {
                Name = "   ",
                Target = new TagTargetDto { Hash = "ignored" },
            },
            new TagDto
            {
                Name = " v1.2.3 ",
                Target = new TagTargetDto { Hash = " abc123 " },
            },
            new TagDto
            {
                Name = "v2.0.0",
                Target = new TagTargetDto { Hash = null },
            },
        ];

        var dto = new TagPageDto
        {
            Values = values,
            Next = "https://example.test/next",
        };

        // Act
        var result = dto.ToDomain();

        // Assert
        result.Values.Should().HaveCount(2);
        result.Values[0].Name.Value.Should().Be("v1.2.3");
        var firstCommitHash = result.Values[0].CommitHash
            ?? throw new InvalidOperationException("Expected commit hash for first mapped tag.");
        firstCommitHash.Value.Should().Be("abc123");
        result.Values[1].Name.Value.Should().Be("v2.0.0");
        result.Values[1].CommitHash.Should().BeNull();
        result.Next.Should().Be(new Uri("https://example.test/next"));
    }

    [Fact(DisplayName = "TransportMappings ToDomain for TagPageDto keeps relative next uri.")]
    [Trait("Category", "Unit")]
    public void ToDomainTagPageKeepsRelativeNextUri()
    {
        // Arrange
        var dto = new TagPageDto
        {
            Values = [],
            Next = "/2.0/repositories/ws/repo/refs/tags?page=2",
        };

        // Act
        var result = dto.ToDomain();

        // Assert
        result.Next.Should().NotBeNull();
        result.Next!.IsAbsoluteUri.Should().BeFalse();
        result.Next.OriginalString.Should().Be("/2.0/repositories/ws/repo/refs/tags?page=2");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for TagPageDto returns null next when empty.")]
    [Trait("Category", "Unit")]
    public void ToDomainTagPageReturnsNullNextWhenEmpty()
    {
        // Arrange
        var dto = new TagPageDto
        {
            Values = [],
            Next = "   ",
        };

        // Act
        var result = dto.ToDomain();

        // Assert
        result.Next.Should().BeNull();
    }

    [Fact(DisplayName = "TransportMappings ToDomain for CommitDto throws when dto is null.")]
    [Trait("Category", "Unit")]
    public void ToDomainCommitThrowsWhenDtoIsNull()
    {
        // Arrange
        CommitDto dto = null!;

        // Act
        Action action = () => _ = dto.ToDomain();

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("dto");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for CommitDto maps trimmed message.")]
    [Trait("Category", "Unit")]
    public void ToDomainCommitMapsTrimmedMessage()
    {
        // Arrange
        var dto = new CommitDto
        {
            Message = "  fix deployment script  ",
        };

        // Act
        var result = dto.ToDomain();

        // Assert
        result.Message.Should().Be("fix deployment script");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraIssueStatusResponseDto throws when dto is null.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraIssueThrowsWhenDtoIsNull()
    {
        // Arrange
        JiraIssueStatusResponseDto dto = null!;

        // Act
        Action action = () => _ = dto.ToDomain("Required Actions", "Breaking Changes");

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("dto");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraIssueStatusResponseDto throws when required actions field name is invalid.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraIssueThrowsWhenRequiredActionsFieldNameIsInvalid()
    {
        // Arrange
        var dto = new JiraIssueStatusResponseDto();

        // Act
        Action action = () => _ = dto.ToDomain(" ", "Breaking Changes");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("requiredActionsFieldName");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraIssueStatusResponseDto throws when breaking changes field name is invalid.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraIssueThrowsWhenBreakingChangesFieldNameIsInvalid()
    {
        // Arrange
        var dto = new JiraIssueStatusResponseDto();

        // Act
        Action action = () => _ = dto.ToDomain("Required Actions", " ");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("breakingChangesFieldName");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraIssueStatusResponseDto maps status title and custom fields.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraIssueMapsStatusTitleAndCustomFields()
    {
        // Arrange
        var dto = new JiraIssueStatusResponseDto
        {
            Fields = new JiraIssueFieldsDto
            {
                Status = new JiraStatusDto
                {
                    Name = " Done ",
                },
                Summary = "  Add API endpoint  ",
                AdditionalFields = new Dictionary<string, JsonElement>
                {
                    ["customfield_10010"] = JsonElementFrom(/*lang=json,strict*/ "{\"content\":[{\"text\":\" Step one \"},{\"value\":\"Step two\"}]}"),
                    ["customfield_10020"] = JsonElementFrom(/*lang=json,strict*/ "[\" Change one \", {\"text\":\"Change two\"}]"),
                },
            },
            Names = new Dictionary<string, string?>
            {
                ["customfield_10010"] = "Required Actions",
                ["customfield_10020"] = "Breaking Changes",
            },
        };

        // Act
        var result = dto.ToDomain("required actions", "breaking changes");

        // Assert
        var statusName = result.StatusName
            ?? throw new InvalidOperationException("Expected status name to be resolved.");
        statusName.Value.Should().Be("Done");
        result.Title.Should().Be("Add API endpoint");
        result.RequiredActionsDetails.Should().Be($"Step one{Environment.NewLine}Step two");
        result.BreakingChangesDetails.Should().Be($"Change one{Environment.NewLine}Change two");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraIssueStatusResponseDto ignores empty Atlassian document metadata in custom fields.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraIssueIgnoresEmptyAtlassianDocumentMetadataInCustomFields()
    {
        // Arrange
        var dto = new JiraIssueStatusResponseDto
        {
            Fields = new JiraIssueFieldsDto
            {
                Status = new JiraStatusDto
                {
                    Name = "Done",
                },
                Summary = "Release fix",
                AdditionalFields = new Dictionary<string, JsonElement>
                {
                    ["customfield_10010"] = JsonElementFrom(/*lang=json,strict*/ "{\"type\":\"doc\",\"version\":1}"),
                    ["customfield_10020"] = JsonElementFrom(/*lang=json,strict*/ "{\"type\":\"doc\",\"version\":1,\"content\":[{\"type\":\"paragraph\",\"content\":[]}]}"),
                },
            },
            Names = new Dictionary<string, string?>
            {
                ["customfield_10010"] = "Required Actions",
                ["customfield_10020"] = "Breaking Changes",
            },
        };

        // Act
        var result = dto.ToDomain("Required Actions", "Breaking Changes");

        // Assert
        result.RequiredActionsDetails.Should().BeNull();
        result.BreakingChangesDetails.Should().BeNull();
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraIssueStatusResponseDto reads Atlassian document text content.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraIssueReadsAtlassianDocumentTextContent()
    {
        // Arrange
        var dto = new JiraIssueStatusResponseDto
        {
            Fields = new JiraIssueFieldsDto
            {
                Status = new JiraStatusDto
                {
                    Name = "Done",
                },
                Summary = "Release fix",
                AdditionalFields = new Dictionary<string, JsonElement>
                {
                    ["customfield_10010"] = JsonElementFrom(
                        /*lang=json,strict*/ "{\"type\":\"doc\",\"version\":1,\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\" Step one \"}]},{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Step two\"}]}]}"),
                },
            },
            Names = new Dictionary<string, string?>
            {
                ["customfield_10010"] = "Required Actions",
            },
        };

        // Act
        var result = dto.ToDomain("Required Actions", "Breaking Changes");

        // Assert
        result.RequiredActionsDetails.Should().Be($"Step one{Environment.NewLine}Step two");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraIssueStatusResponseDto uses defaults when status and summary are missing.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraIssueUsesDefaultsWhenStatusAndSummaryAreMissing()
    {
        // Arrange
        var dto = new JiraIssueStatusResponseDto
        {
            Fields = new JiraIssueFieldsDto
            {
                Status = new JiraStatusDto
                {
                    Name = "   ",
                },
                Summary = "   ",
            },
        };

        // Act
        var result = dto.ToDomain("Required Actions", "Breaking Changes");

        // Assert
        result.StatusName.Should().BeNull();
        result.Title.Should().Be("N/A");
        result.RequiredActionsDetails.Should().BeNull();
        result.BreakingChangesDetails.Should().BeNull();
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraSearchResponseDto throws when dto is null.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraSearchThrowsWhenDtoIsNull()
    {
        // Arrange
        JiraSearchResponseDto dto = null!;

        // Act
        Action action = () => _ = dto.ToDomain();

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("dto");
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraSearchResponseDto returns empty list when issues are null.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraSearchReturnsEmptyListWhenIssuesAreNull()
    {
        // Arrange
        var dto = new JiraSearchResponseDto
        {
            Issues = null!,
        };

        // Act
        var result = dto.ToDomain();

        // Assert
        result.Issues.Should().BeEmpty();
    }

    [Fact(DisplayName = "TransportMappings ToDomain for JiraSearchResponseDto maps issues and skips null entries.")]
    [Trait("Category", "Unit")]
    public void ToDomainJiraSearchMapsIssuesAndSkipsNullEntries()
    {
        // Arrange
        IReadOnlyList<JiraSearchIssueDto> issues =
        [
            null!,
            new JiraSearchIssueDto
            {
                Fields = new JiraIssueFieldsDto
                {
                    Status = new JiraStatusDto { Name = "In Progress" },
                    Summary = "  Deploy service  ",
                },
            },
            new JiraSearchIssueDto
            {
                Fields = new JiraIssueFieldsDto
                {
                    Status = new JiraStatusDto { Name = "   " },
                    Summary = "   ",
                },
            },
        ];

        var dto = new JiraSearchResponseDto
        {
            Issues = issues,
        };

        // Act
        var result = dto.ToDomain();

        // Assert
        result.Issues.Should().HaveCount(2);
        var firstStatusName = result.Issues[0].StatusName
            ?? throw new InvalidOperationException("Expected first issue to have a status.");
        firstStatusName.Value.Should().Be("In Progress");
        result.Issues[0].Title.Should().Be("Deploy service");
        result.Issues[1].StatusName.Should().BeNull();
        result.Issues[1].Title.Should().Be("N/A");
        result.Issues[1].RequiredActionsDetails.Should().BeNull();
        result.Issues[1].BreakingChangesDetails.Should().BeNull();
    }

    private static JsonElement JsonElementFrom(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
