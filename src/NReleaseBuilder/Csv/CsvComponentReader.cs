using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

using NReleaseBuilder.Abstractions.Csv;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Csv;

/// <summary>
/// CSV reader for extracting component, repository and version information.
/// </summary>
public sealed class CsvComponentReader : ICsvComponentReader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvComponentReader"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    /// <param name="renderer">Application renderer.</param>
    public CsvComponentReader(
        IOptions<AppSettings> options,
        IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(renderer);

        var settings = options.Value;
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.CsvFilePath);

        _csvFilePath = settings.CsvFilePath;
        _componentNamesFilter = BuildComponentNamesFilter(settings.CsvComponentNamesFilter);
        _renderer = renderer;
    }

    /// <inheritdoc />
    public IReadOnlyList<ComponentRow>? Read()
    {
        try
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

                if (!IsComponentIncluded(component))
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
        catch (MalformedLineException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (IOException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (ArgumentException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
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

    private bool IsComponentIncluded(string component)
        => _componentNamesFilter.Count == 0 || _componentNamesFilter.Contains(component);

    private static HashSet<string> BuildComponentNamesFilter(IReadOnlyList<string>? componentNamesFilter)
    {
        if (componentNamesFilter is null || componentNamesFilter.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var componentName in componentNamesFilter)
        {
            if (string.IsNullOrWhiteSpace(componentName))
            {
                continue;
            }

            _ = result.Add(componentName.Trim());
        }

        return result;
    }

    private void PrintCsvParsingError(Exception exception)
    {
        _renderer.PrintError(
            new ErrorMessage($"Failed to parse CSV: {exception.Message}"));
    }

    private readonly string _csvFilePath;
    private readonly HashSet<string> _componentNamesFilter;
    private readonly IRenderer _renderer;
}
