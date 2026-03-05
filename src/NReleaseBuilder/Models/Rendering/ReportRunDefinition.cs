namespace NReleaseBuilder.Models.Rendering;

/// <summary>
/// Definition of a single report run.
/// </summary>
public sealed record ReportRunDefinition(
    string? Name,
    IReadOnlyList<string>? ComponentNamesFilter,
    string? PdfOutputPathOverride,
    string? ExcelOutputPathOverride);
