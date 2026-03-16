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
    private readonly record struct CsvImageRow(
        ComponentName Component,
        RepositoryName Repository,
        VersionLabel Version);

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

        _targetCsvFilePath = settings.TargetCsvFilePath.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(_targetCsvFilePath);

        _devCsvFilePath = settings.DevCsvFilePath.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(_devCsvFilePath);
        _defaultComponentNamesFilter = BuildComponentNamesFilter(settings.CsvComponentNamesFilter);
        _renderer = renderer;
    }

    /// <inheritdoc />
    public IReadOnlyList<ComponentRow>? Read(IReadOnlyList<string>? componentNamesFilter = null)
    {
        try
        {
            var effectiveFilter = componentNamesFilter is null
                ? _defaultComponentNamesFilter
                : BuildComponentNamesFilter(componentNamesFilter);

            var targetRowsByComponent = ReadRowsFromCsv(_targetCsvFilePath, effectiveFilter);
            var devRowsByComponent = ReadDevRows(effectiveFilter);
            var rows = MergeRows(targetRowsByComponent, devRowsByComponent);

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

    /// <inheritdoc />
    public ComponentSourceSnapshot? ReadSourceSnapshot()
    {
        try
        {
            var noFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var devRowsByComponent = ReadRowsFromCsv(_devCsvFilePath, noFilter);
            var targetRowsByComponent = ReadRowsFromCsv(_targetCsvFilePath, noFilter);

            return new ComponentSourceSnapshot(
                [.. devRowsByComponent.Values
                    .Select(static x => x.Component)
                    .OrderBy(static x => x.Value, StringComparer.OrdinalIgnoreCase)],
                [.. targetRowsByComponent.Values
                    .Select(static x => x.Component)
                    .OrderBy(static x => x.Value, StringComparer.OrdinalIgnoreCase)]);
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

    private Dictionary<string, CsvImageRow> ReadDevRows(HashSet<string> effectiveFilter)
    {
        if (string.Equals(_devCsvFilePath, _targetCsvFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, CsvImageRow>(StringComparer.OrdinalIgnoreCase);
        }

        return ReadRowsFromCsv(_devCsvFilePath, effectiveFilter);
    }

    private static List<ComponentRow> MergeRows(
        IReadOnlyDictionary<string, CsvImageRow> targetRowsByComponent,
        IReadOnlyDictionary<string, CsvImageRow> devRowsByComponent)
    {
        var rows = new List<ComponentRow>(targetRowsByComponent.Count + devRowsByComponent.Count);

        foreach (var targetRow in targetRowsByComponent.Values)
        {
            rows.Add(new ComponentRow(
                targetRow.Component,
                targetRow.Repository,
                targetRow.Version));
        }

        foreach (var (componentName, devRow) in devRowsByComponent)
        {
            if (targetRowsByComponent.ContainsKey(componentName))
            {
                continue;
            }

            rows.Add(new ComponentRow(
                devRow.Component,
                devRow.Repository,
                devRow.Version,
                isReleased: false));
        }

        return rows;
    }

    private static Dictionary<string, CsvImageRow> ReadRowsFromCsv(string csvFilePath, HashSet<string> effectiveFilter)
    {
        var rows = new Dictionary<string, CsvImageRow>(StringComparer.OrdinalIgnoreCase);

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

            if (!IsComponentIncluded(component, effectiveFilter))
            {
                continue;
            }

            var (repository, version) = ParseImage(image);
            if (string.IsNullOrWhiteSpace(repository) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            rows[component] = new CsvImageRow(
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

    private static bool IsComponentIncluded(string component, HashSet<string> componentNamesFilter)
        => componentNamesFilter.Count == 0 || componentNamesFilter.Contains(component);

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

    private readonly string _targetCsvFilePath;
    private readonly string _devCsvFilePath;
    private readonly HashSet<string> _defaultComponentNamesFilter;
    private readonly IRenderer _renderer;
}
