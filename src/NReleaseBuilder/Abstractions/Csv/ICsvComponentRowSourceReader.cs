using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Abstractions.Csv;

/// <summary>
/// Reads component rows from a CSV source file.
/// </summary>
public interface ICsvComponentRowSourceReader
{
    /// <summary>
    /// Reads distinct component rows from a CSV source file.
    /// </summary>
    /// <param name="csvFilePath">CSV file path.</param>
    /// <param name="componentNamesFilter">Component allow-list filter.</param>
    /// <returns>Rows keyed by component name.</returns>
    IReadOnlyDictionary<string, ComponentRow> ReadRows(
        string csvFilePath,
        IReadOnlySet<string> componentNamesFilter);
}
