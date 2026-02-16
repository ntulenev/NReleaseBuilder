using Microsoft.VisualBasic.FileIO;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;

using Microsoft.Extensions.Options;

namespace NReleaseBuilder.Services;

/// <summary>
/// CSV reader for extracting component, repository and version information.
/// </summary>
public sealed class CsvComponentReader : ICsvComponentReader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvComponentReader"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    public CsvComponentReader(IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.CsvFilePath);

        _csvFilePath = settings.CsvFilePath;
    }

    /// <inheritdoc />
    public IReadOnlyList<ComponentRow> Read()
    {
        var rows = new HashSet<ComponentRow>();

        using var parser = new TextFieldParser(_csvFilePath);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;

        var headers = parser.ReadFields();
        if (headers is null)
        {
            throw new InvalidOperationException("CSV file is empty.");
        }

        var containerIndex = FindHeaderIndex(headers, "container");
        var imageIndex = FindHeaderIndex(headers, "image");

        if (containerIndex < 0 || imageIndex < 0)
        {
            throw new InvalidOperationException("CSV must contain 'container' and 'image' columns.");
        }

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length <= Math.Max(containerIndex, imageIndex))
            {
                continue;
            }

            var component = fields[containerIndex]?.Trim();
            var image = fields[imageIndex]?.Trim();

            if (string.IsNullOrWhiteSpace(component) || string.IsNullOrWhiteSpace(image))
            {
                continue;
            }

            var (repository, version) = ParseImage(image);
            if (string.IsNullOrWhiteSpace(repository) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            _ = rows.Add(new ComponentRow(
                new ComponentName(component),
                new RepositoryName(repository),
                new VersionLabel(version)));
        }

        return
        [
            .. rows.OrderBy(x => x.Component.Value, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static int FindHeaderIndex(string[] headers, string headerName)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            if (string.Equals(headers[i], headerName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static (string Repository, string Version) ParseImage(string image)
    {
        var imageWithoutDigest = image.Split('@', 2)[0];
        var lastSlashIndex = imageWithoutDigest.LastIndexOf('/');
        var tagSeparatorIndex = imageWithoutDigest.LastIndexOf(':');

        if (tagSeparatorIndex > lastSlashIndex)
        {
            var repository = imageWithoutDigest.Substring(lastSlashIndex + 1, tagSeparatorIndex - lastSlashIndex - 1);
            var version = imageWithoutDigest[(tagSeparatorIndex + 1)..];
            return (repository, version);
        }

        return (imageWithoutDigest[(lastSlashIndex + 1)..], string.Empty);
    }

    private readonly string _csvFilePath;
}
