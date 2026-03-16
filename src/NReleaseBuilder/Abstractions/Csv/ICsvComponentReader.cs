

using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Abstractions.Csv;

/// <summary>
/// Reads component/version rows from CSV input.
/// </summary>
public interface ICsvComponentReader
{
    /// <summary>
    /// Reads rows from a configured CSV file.
    /// </summary>
    /// <param name="componentNamesFilter">
    /// Optional component names allow-list; when <see langword="null"/>, configured default filter is used.
    /// </param>
    /// <returns>Distinct component rows; <see langword="null"/> when reading fails.</returns>
    IReadOnlyList<ComponentRow>? Read(IReadOnlyList<string>? componentNamesFilter = null);

    /// <summary>
    /// Reads unfiltered component presence from development and target CSV inputs.
    /// </summary>
    /// <returns>Source snapshot; <see langword="null"/> when reading fails.</returns>
    ComponentSourceSnapshot? ReadSourceSnapshot();
}
