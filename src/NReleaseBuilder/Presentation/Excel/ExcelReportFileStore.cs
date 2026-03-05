using Microsoft.Extensions.Options;

using MiniExcelLibs;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;

namespace NReleaseBuilder.Presentation.Excel;

/// <summary>
/// Filesystem-backed Excel report persistence.
/// </summary>
public sealed class ExcelReportFileStore : IExcelReportFileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelReportFileStore"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    public ExcelReportFileStore(IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _settings = options.Value;
    }

    /// <inheritdoc />
    public MemoryStream CreateWorkbookStream(IReadOnlyDictionary<string, object> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);

        var outputStream = new MemoryStream();
        _ = MiniExcel.SaveAs(outputStream, sheets, printHeader: false);
        outputStream.Position = 0;
        return outputStream;
    }

    /// <inheritdoc />
    public string Save(Stream contentStream, string? outputPathOverride = null)
    {
        ArgumentNullException.ThrowIfNull(contentStream);

        var outputPath = _settings.Excel.ResolveOutputPath(outputPathOverride);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            _ = Directory.CreateDirectory(outputDirectory);
        }

        contentStream.Position = 0;
        using var fileStream = File.Create(outputPath);
        contentStream.CopyTo(fileStream);
        return outputPath;
    }

    private readonly AppSettings _settings;
}
