using QuestPDF.Fluent;

using QContainer = QuestPDF.Infrastructure.IContainer;

namespace NReleaseBuilder.Presentation.Pdf;

/// <summary>
/// Shared helper functions for PDF-specific presentation styling.
/// </summary>
internal static class PdfPresentationHelpers
{
    /// <summary>
    /// Styles a PDF table header cell.
    /// </summary>
    /// <param name="container">Container to style.</param>
    /// <returns>Styled container.</returns>
    public static QContainer StylePdfHeaderCell(QContainer container) =>
        container
            .Background("#f3f4f6")
            .Border(1)
            .BorderColor("#d1d5db")
            .PaddingHorizontal(6)
            .PaddingVertical(4);

    /// <summary>
    /// Styles a PDF table body cell.
    /// </summary>
    /// <param name="container">Container to style.</param>
    /// <returns>Styled container.</returns>
    public static QContainer StylePdfBodyCell(QContainer container) =>
        container
            .BorderBottom(1)
            .BorderColor("#e5e7eb")
            .PaddingHorizontal(6)
            .PaddingVertical(4);

    /// <summary>
    /// Styles a PDF alert details box.
    /// </summary>
    /// <param name="container">Container to style.</param>
    /// <returns>Styled container.</returns>
    public static QContainer StylePdfAlertDetailsBox(QContainer container) =>
        container
            .Border(1)
            .BorderColor("#d1d5db")
            .Background("#f9fafb")
            .PaddingHorizontal(6)
            .PaddingVertical(5);

    /// <summary>
    /// Resolves color for releases-ahead counter in PDF output.
    /// </summary>
    /// <param name="newerVersionCount">Count of newer versions.</param>
    /// <returns>Hex color code.</returns>
    public static string ResolveAheadCounterHexColor(int newerVersionCount)
    {
        return newerVersionCount switch
        {
            <= 0 => "#6b7280",
            <= 2 => "#ca8a04",
            <= 5 => "#ea580c",
            _ => "#b91c1c",
        };
    }
}
