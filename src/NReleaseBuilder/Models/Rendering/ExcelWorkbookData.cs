
namespace NReleaseBuilder.Models.Rendering;

/// <summary>
/// Excel workbook data and formatting metadata.
/// </summary>
public sealed record ExcelWorkbookData(
    IReadOnlyDictionary<string, object> Sheets,
    IReadOnlyDictionary<string, ExcelSheetLayout> Layouts);
