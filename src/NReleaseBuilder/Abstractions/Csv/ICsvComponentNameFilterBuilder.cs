namespace NReleaseBuilder.Abstractions.Csv;

/// <summary>
/// Builds case-insensitive component-name filters for CSV reads.
/// </summary>
public interface ICsvComponentNameFilterBuilder
{
    /// <summary>
    /// Builds an effective component-name filter set.
    /// </summary>
    /// <param name="componentNamesFilter">Optional component names.</param>
    /// <returns>Case-insensitive component-name set.</returns>
    IReadOnlySet<string> Build(IReadOnlyList<string>? componentNamesFilter);
}
