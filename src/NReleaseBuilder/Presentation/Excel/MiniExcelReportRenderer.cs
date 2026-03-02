using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Presentation.Excel;

/// <summary>
/// MiniExcel-based implementation for Excel report rendering.
/// </summary>
public sealed class MiniExcelReportRenderer : IExcelReportRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MiniExcelReportRenderer"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    /// <param name="excelContentComposer">Excel workbook content composer.</param>
    /// <param name="excelReportFileStore">Excel output persistence service.</param>
    /// <param name="workbookFormatter">Workbook formatting service.</param>
    public MiniExcelReportRenderer(
        IOptions<AppSettings> options,
        IExcelContentComposer excelContentComposer,
        IExcelReportFileStore excelReportFileStore,
        IWorkbookFormatter workbookFormatter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(excelContentComposer);
        ArgumentNullException.ThrowIfNull(excelReportFileStore);
        ArgumentNullException.ThrowIfNull(workbookFormatter);

        _settings = options.Value;
        _excelContentComposer = excelContentComposer;
        _excelReportFileStore = excelReportFileStore;
        _workbookFormatter = workbookFormatter;
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

        if (!_settings.Excel.Enabled)
        {
            return;
        }

        var workbook = _excelContentComposer.ComposeWorkbook(rows, allowedStatuses, statusStatistics);
        using var outputStream = _excelReportFileStore.CreateWorkbookStream(workbook.Sheets);
        _workbookFormatter.Format(outputStream, workbook.Layouts);
        var outputPath = _excelReportFileStore.Save(outputStream);

        System.Console.WriteLine($"Excel report saved to: {outputPath}");
    }

    private readonly AppSettings _settings;
    private readonly IExcelContentComposer _excelContentComposer;
    private readonly IExcelReportFileStore _excelReportFileStore;
    private readonly IWorkbookFormatter _workbookFormatter;
}
