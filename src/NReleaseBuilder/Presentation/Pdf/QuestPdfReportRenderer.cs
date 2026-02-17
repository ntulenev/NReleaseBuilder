using System.Globalization;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;

using QuestPDF.Fluent;
using QuestPDF.Helpers;

using QLicenseType = QuestPDF.Infrastructure.LicenseType;

namespace NReleaseBuilder.Presentation.Pdf;

/// <summary>
/// QuestPDF implementation for PDF report rendering.
/// </summary>
public sealed class QuestPdfReportRenderer : IPdfReportRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestPdfReportRenderer"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    /// <param name="pdfReportFileStore">PDF output persistence service.</param>
    /// <param name="pdfContentComposer">PDF page content composer.</param>
    public QuestPdfReportRenderer(
        IOptions<AppSettings> options,
        IPdfReportFileStore pdfReportFileStore,
        IPdfContentComposer pdfContentComposer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pdfReportFileStore);
        ArgumentNullException.ThrowIfNull(pdfContentComposer);
        _settings = options.Value;
        _pdfReportFileStore = pdfReportFileStore;
        _pdfContentComposer = pdfContentComposer;
    }

    /// <inheritdoc />
    public void RenderReport(
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(allowedStatuses);
        ArgumentNullException.ThrowIfNull(statusStatistics);

        if (!_settings.Pdf.Enabled)
        {
            return;
        }

        var outputPath = _settings.Pdf.ResolveOutputPath();

        QuestPDF.Settings.License = QLicenseType.Community;

        var document = Document
            .Create(container =>
            {
                _ = container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(static style => style.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Spacing(2);
                        _ = column.Item().Text("Components Version Check").Bold().FontSize(16);
                        _ = column.Item().Text(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Generated: {0:yyyy-MM-dd HH:mm:ss zzz}",
                                DateTimeOffset.Now));
                        _ = column.Item().Text("Source: " + _settings.CsvFilePath);
                        _ = column.Item().Text("Workspace: " + _settings.Bitbucket.Workspace);

                        if (allowedStatuses.Length > 0)
                        {
                            _ = column.Item().Text(
                                "Jira Status Filter: "
                                + string.Join(", ", allowedStatuses.Select(static x => x.Value)));
                        }
                    });

                    page.Content().PaddingTop(8).Column(column =>
                    _pdfContentComposer.ComposeContent(column, rows, allowedStatuses, statusStatistics));

                    page.Footer().AlignRight().Text(text =>
                    {
                        _ = text.Span("Page ");
                        _ = text.CurrentPageNumber();
                        _ = text.Span(" / ");
                        _ = text.TotalPages();
                    });
                });
            });

        _pdfReportFileStore.Save(outputPath, document);

        System.Console.WriteLine($"PDF report saved to: {outputPath}");
    }

    private readonly AppSettings _settings;
    private readonly IPdfReportFileStore _pdfReportFileStore;
    private readonly IPdfContentComposer _pdfContentComposer;
}
