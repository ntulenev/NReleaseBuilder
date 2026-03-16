using NReleaseBuilder.Abstractions.Csv;

namespace NReleaseBuilder.Csv;

/// <summary>
/// Parses repository/version values from container image references.
/// </summary>
public sealed class CsvImageParser : ICsvImageParser
{
    /// <inheritdoc />
    public bool TryParse(string image, out string repository, out string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(image);

        var imageWithoutDigest = image.Split('@', 2)[0];
        var lastSlashIndex = imageWithoutDigest.LastIndexOf('/');
        var tagSeparatorIndex = imageWithoutDigest.LastIndexOf(':');

        if (tagSeparatorIndex <= lastSlashIndex)
        {
            repository = string.Empty;
            version = string.Empty;
            return false;
        }

        repository = imageWithoutDigest.Substring(
            lastSlashIndex + 1,
            tagSeparatorIndex - lastSlashIndex - 1);
        version = imageWithoutDigest[(tagSeparatorIndex + 1)..];

        return !string.IsNullOrWhiteSpace(repository)
            && !string.IsNullOrWhiteSpace(version);
    }
}
