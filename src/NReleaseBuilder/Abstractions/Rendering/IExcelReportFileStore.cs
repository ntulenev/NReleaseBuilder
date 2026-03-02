
namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// Abstraction for persisting generated Excel report content.
/// </summary>
public interface IExcelReportFileStore
{
    /// <summary>
    /// Creates an Excel workbook stream from the provided sheet data.
    /// </summary>
    /// <param name="sheets">Workbook sheet data keyed by sheet name.</param>
    /// <returns>A readable and writable workbook stream.</returns>
    MemoryStream CreateWorkbookStream(IReadOnlyDictionary<string, object> sheets);

    /// <summary>
    /// Saves Excel report content using the configured output path.
    /// </summary>
    /// <param name="contentStream">Excel content stream.</param>
    /// <returns>The resolved output path.</returns>
    string Save(Stream contentStream);
}
