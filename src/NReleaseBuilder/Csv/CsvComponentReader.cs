using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;

using NReleaseBuilder.Abstractions.Csv;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
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
    /// <param name="componentNameFilterBuilder">Component-name filter builder.</param>
    /// <param name="rowSourceReader">CSV row source reader.</param>
    /// <param name="rowsMerger">Target/dev rows merger.</param>
    public CsvComponentReader(
        IOptions<AppSettings> options,
        IRenderer renderer,
        ICsvComponentNameFilterBuilder componentNameFilterBuilder,
        ICsvComponentRowSourceReader rowSourceReader,
        ICsvComponentRowsMerger rowsMerger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(componentNameFilterBuilder);
        ArgumentNullException.ThrowIfNull(rowSourceReader);
        ArgumentNullException.ThrowIfNull(rowsMerger);

        var settings = options.Value;

        _targetCsvFilePath = settings.TargetCsvFilePath.Trim();
        _devCsvFilePath = settings.DevCsvFilePath.Trim();
        _defaultComponentNamesFilter = componentNameFilterBuilder.Build(settings.CsvComponentNamesFilter);
        _componentNameFilterBuilder = componentNameFilterBuilder;
        _rowSourceReader = rowSourceReader;
        _rowsMerger = rowsMerger;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public IReadOnlyList<ComponentRow>? Read(IReadOnlyList<string>? componentNamesFilter = null)
    {
        try
        {
            var effectiveFilter = componentNamesFilter is null
                ? _defaultComponentNamesFilter
                : _componentNameFilterBuilder.Build(componentNamesFilter);

            var targetRowsByComponent = _rowSourceReader.ReadRows(_targetCsvFilePath, effectiveFilter);
            var devRowsByComponent = ReadDevRows(effectiveFilter);
            var rows = _rowsMerger.Merge(targetRowsByComponent, devRowsByComponent);

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
            var noFilter = _componentNameFilterBuilder.Build(null);
            var devRowsByComponent = _rowSourceReader.ReadRows(_devCsvFilePath, noFilter);
            var targetRowsByComponent = _rowSourceReader.ReadRows(_targetCsvFilePath, noFilter);

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

    private IReadOnlyDictionary<string, ComponentRow> ReadDevRows(IReadOnlySet<string> effectiveFilter)
    {
        if (string.Equals(_devCsvFilePath, _targetCsvFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, ComponentRow>(StringComparer.OrdinalIgnoreCase);
        }

        return _rowSourceReader.ReadRows(_devCsvFilePath, effectiveFilter);
    }

    private void PrintCsvParsingError(Exception exception)
    {
        _renderer.PrintError(
            new ErrorMessage($"Failed to parse CSV: {exception.Message}"));
    }

    private readonly string _targetCsvFilePath;
    private readonly string _devCsvFilePath;
    private readonly IReadOnlySet<string> _defaultComponentNamesFilter;
    private readonly ICsvComponentNameFilterBuilder _componentNameFilterBuilder;
    private readonly ICsvComponentRowSourceReader _rowSourceReader;
    private readonly ICsvComponentRowsMerger _rowsMerger;
    private readonly IRenderer _renderer;
}
