using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Models.Rendering;

namespace NReleaseBuilder.Presentation;

/// <summary>
/// In-memory implementation for current report run context.
/// </summary>
public sealed class ReportRunContextAccessor : IReportRunContextAccessor
{
    /// <inheritdoc />
    public void Setup(ReportRunDefinition reportRunDefinition)
    {
        ArgumentNullException.ThrowIfNull(reportRunDefinition);
        Current = new ReportRunContext(
            reportRunDefinition.Name,
            reportRunDefinition.PdfOutputPathOverride,
            reportRunDefinition.ExcelOutputPathOverride);
    }

    /// <inheritdoc />
    public ReportRunContext Current { get; private set; } = ReportRunContext.Empty;

    /// <inheritdoc />
    public void Reset() => Current = ReportRunContext.Empty;
}
