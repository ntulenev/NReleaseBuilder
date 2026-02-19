using FluentAssertions;

using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Presentation.Pdf;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NReleaseBuilder.Tests.Presentation.Pdf;

public class PdfPresentationHelpersTests
{
    [Fact(DisplayName = "PdfPresentationHelpers ResolveAheadCounterHexColor returns expected colors by thresholds.")]
    [Trait("Category", "Unit")]
    public void ResolveAheadCounterHexColorReturnsExpectedColorsByThresholds()
    {
        // Arrange
        // Act
        var zero = PdfPresentationHelpers.ResolveAheadCounterHexColor(0);
        var low = PdfPresentationHelpers.ResolveAheadCounterHexColor(2);
        var medium = PdfPresentationHelpers.ResolveAheadCounterHexColor(5);
        var high = PdfPresentationHelpers.ResolveAheadCounterHexColor(6);

        // Assert
        zero.Should().Be("#6b7280");
        low.Should().Be("#ca8a04");
        medium.Should().Be("#ea580c");
        high.Should().Be("#b91c1c");
    }

    [Fact(DisplayName = "PdfPresentationHelpers ResolveCheckStatusHexColor returns expected status colors.")]
    [Trait("Category", "Unit")]
    public void ResolveCheckStatusHexColorReturnsExpectedStatusColors()
    {
        // Arrange
        var unknownStatus = (CheckStatus)999;

        // Act
        var upToDate = PdfPresentationHelpers.ResolveCheckStatusHexColor(CheckStatus.UpToDate, newerVersionCount: 0);
        var outdatedLow = PdfPresentationHelpers.ResolveCheckStatusHexColor(CheckStatus.Outdated, newerVersionCount: 1);
        var outdatedHigh = PdfPresentationHelpers.ResolveCheckStatusHexColor(CheckStatus.Outdated, newerVersionCount: 6);
        var notFound = PdfPresentationHelpers.ResolveCheckStatusHexColor(CheckStatus.RepositoryNotFound, newerVersionCount: 0);
        var unknown = PdfPresentationHelpers.ResolveCheckStatusHexColor(unknownStatus, newerVersionCount: 0);

        // Assert
        upToDate.Should().Be("#15803d");
        outdatedLow.Should().Be("#ea580c");
        outdatedHigh.Should().Be("#b91c1c");
        notFound.Should().Be("#b91c1c");
        unknown.Should().Be("#6b7280");
    }

    [Fact(DisplayName = "PdfPresentationHelpers style helpers can be applied during PDF generation.")]
    [Trait("Category", "Unit")]
    public void StyleHelpersCanBeAppliedDuringPdfGeneration()
    {
        // Arrange
        QuestPDF.Settings.License = LicenseType.Community;

        // Act
        var exception = Record.Exception(() =>
        {
            var pdfBytes = Document
                .Create(container =>
                {
                    _ = container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(10);
                        page.Content().Column(column =>
                        {
                            _ = column.Item().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Header");
                            _ = column.Item().Element(PdfPresentationHelpers.StylePdfBodyCell).Text("Body");
                            _ = column.Item().Element(PdfPresentationHelpers.StylePdfAlertDetailsBox).Text("Alert");
                        });
                    });
                })
                .GeneratePdf();

            pdfBytes.Should().NotBeEmpty();
        });

        // Assert
        exception.Should().BeNull();
    }
}
