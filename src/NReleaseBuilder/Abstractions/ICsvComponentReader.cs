using NReleaseBuilder.Models;

namespace NReleaseBuilder.Abstractions;

/// <summary>
/// Reads component/version rows from CSV input.
/// </summary>
public interface ICsvComponentReader
{
    /// <summary>
    /// Reads rows from a configured CSV file.
    /// </summary>
    /// <returns>Distinct component rows.</returns>
    IReadOnlyList<ComponentRow> Read();
}
