using FluentAssertions;

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
