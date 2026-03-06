using NReleaseBuilder.Models.Rendering;

namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// Mutable accessor for the current report run context.
/// </summary>
public interface IReportRunContextAccessor
{
    /// <summary>
    /// Sets up current context from the report run definition.
    /// </summary>
    /// <param name="reportRunDefinition">Report run definition.</param>
    void Setup(ReportRunDefinition reportRunDefinition);

    /// <summary>
    /// Gets current run context.
    /// </summary>
    ReportRunContext Current { get; }

    /// <summary>
    /// Resets run context to default values.
    /// </summary>
    void Reset();
}
