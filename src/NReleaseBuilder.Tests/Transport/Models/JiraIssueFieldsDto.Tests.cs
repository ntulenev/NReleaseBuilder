using System.Text.Json;

using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class JiraIssueFieldsDtoTests
{
    [Fact(DisplayName = "JiraIssueFieldsDto properties can be assigned.")]
    [Trait("Category", "Unit")]
    public void PropertiesCanBeAssigned()
    {
        // Arrange
        var additionalFields = new Dictionary<string, JsonElement>
        {
            ["customfield_1"] = JsonElementFrom("\"value\""),
        };

        var dto = new JiraIssueFieldsDto
        {
            Status = new JiraStatusDto { Name = "Done" },
            Summary = "Add endpoint",
            AdditionalFields = additionalFields,
        };

        // Act
        var statusName = dto.Status?.Name;
        var summary = dto.Summary;
        var fieldCount = dto.AdditionalFields?.Count;

        // Assert
        statusName.Should().Be("Done");
        summary.Should().Be("Add endpoint");
        fieldCount.Should().Be(1);
    }

    [Fact(DisplayName = "JiraIssueFieldsDto captures unknown json fields as extension data.")]
    [Trait("Category", "Unit")]
    public void CapturesUnknownJsonFieldsAsExtensionData()
    {
        // Arrange
        const string json = """
                            {
                              "Status": { "Name": "In Progress" },
                              "Summary": "Investigate incident",
                              "customfield_10010": { "text": "Follow runbook" }
                            }
                            """;

        // Act
        var dto = JsonSerializer.Deserialize<JiraIssueFieldsDto>(json);

        // Assert
        dto.Should().NotBeNull();
        dto!.Status.Should().NotBeNull();
        dto.Status!.Name.Should().Be("In Progress");
        dto.Summary.Should().Be("Investigate incident");
        dto.AdditionalFields.Should().NotBeNull();
        dto.AdditionalFields!.ContainsKey("customfield_10010").Should().BeTrue();
    }

    private static JsonElement JsonElementFrom(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
