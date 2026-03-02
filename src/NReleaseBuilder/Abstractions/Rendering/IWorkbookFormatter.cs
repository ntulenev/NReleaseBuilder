
using NReleaseBuilder.Models.Rendering;

namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// Applies workbook formatting metadata to an Excel workbook stream.
/// </summary>
public interface IWorkbookFormatter
{
    /// <summary>
    /// Applies formatting to the workbook stream.
    /// </summary>
    /// <param name="workbookStream">Workbook stream to format.</param>
    /// <param name="layouts">Worksheet layout metadata keyed by sheet name.</param>
    void Format(Stream workbookStream, IReadOnlyDictionary<string, ExcelSheetLayout> layouts);
}
