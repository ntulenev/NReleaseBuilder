using Microsoft.VisualBasic.FileIO;

using NReleaseBuilder.Abstractions.Csv;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Csv;

/// <summary>
/// Reads component rows from CSV source files.
/// </summary>
public sealed class CsvComponentRowSourceReader : ICsvComponentRowSourceReader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvComponentRowSourceReader"/> class.
    /// </summary>
    /// <param name="imageParser">CSV image parser.</param>
    public CsvComponentRowSourceReader(ICsvImageParser imageParser)
    {
        ArgumentNullException.ThrowIfNull(imageParser);
        _imageParser = imageParser;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ComponentRow> ReadRows(
        string csvFilePath,
        IReadOnlySet<string> componentNamesFilter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvFilePath);
        ArgumentNullException.ThrowIfNull(componentNamesFilter);

        var rows = new Dictionary<string, ComponentRow>(StringComparer.OrdinalIgnoreCase);

        using var parser = new TextFieldParser(csvFilePath);
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

            if (componentNamesFilter.Count > 0 && !componentNamesFilter.Contains(component))
            {
                continue;
            }

            if (!_imageParser.TryParse(image, out var repository, out var version))
            {
                continue;
            }

            rows[component] = new ComponentRow(
                new ComponentName(component),
                new RepositoryName(repository),
                new VersionLabel(version));
        }

        return rows;
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

    private readonly ICsvImageParser _imageParser;
}
