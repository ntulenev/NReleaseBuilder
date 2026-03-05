namespace NReleaseBuilder.Models.Rendering;

/// <summary>
/// Runtime context for a single report generation pass.
/// </summary>
public sealed record ReportRunContext(
    string? GroupName = null,
    string? PdfOutputPathOverride = null,
    string? ExcelOutputPathOverride = null)
{
    /// <summary>
    /// Empty/default run context.
    /// </summary>
    public static ReportRunContext Empty { get; } = new();
}
