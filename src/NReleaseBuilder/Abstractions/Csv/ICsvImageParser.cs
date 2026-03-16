namespace NReleaseBuilder.Abstractions.Csv;

/// <summary>
/// Parses repository/version data from a CSV image reference.
/// </summary>
public interface ICsvImageParser
{
    /// <summary>
    /// Attempts to parse repository and version values from an image reference.
    /// </summary>
    /// <param name="image">Image reference.</param>
    /// <param name="repository">Parsed repository name.</param>
    /// <param name="version">Parsed version label.</param>
    /// <returns><see langword="true"/> when both values were parsed.</returns>
    bool TryParse(string image, out string repository, out string version);
}
