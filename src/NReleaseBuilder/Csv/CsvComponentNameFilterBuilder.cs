using NReleaseBuilder.Abstractions.Csv;

namespace NReleaseBuilder.Csv;

/// <summary>
/// Builds normalized component-name filters for CSV reads.
/// </summary>
public sealed class CsvComponentNameFilterBuilder : ICsvComponentNameFilterBuilder
{
    /// <inheritdoc />
    public IReadOnlySet<string> Build(IReadOnlyList<string>? componentNamesFilter)
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
}
