using NReleaseBuilder.Models;

namespace NReleaseBuilder.Abstractions;

/// <summary>
/// Reads component/version rows from CSV input.
/// </summary>
public interface ICsvComponentReader
{
    /// <summary>
    /// Reads rows from a CSV file.
    /// </summary>
    /// <param name="csvFilePath">CSV file path.</param>
    /// <returns>Distinct component rows.</returns>
    IReadOnlyList<ComponentRow> Read(string csvFilePath);
}
