using NReleaseBuilder.Models;

namespace NReleaseBuilder.Abstractions.Csv;

/// <summary>
/// Reads component/version rows from CSV input.
/// </summary>
public interface ICsvComponentReader
{
    /// <summary>
    /// Reads rows from a configured CSV file.
    /// </summary>
    /// <returns>Distinct component rows; <see langword="null"/> when reading fails.</returns>
    IReadOnlyList<ComponentRow>? Read();
}
