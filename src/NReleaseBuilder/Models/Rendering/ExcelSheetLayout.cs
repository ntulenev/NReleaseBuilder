
using System.Collections.ObjectModel;

namespace NReleaseBuilder.Models.Rendering;

/// <summary>
/// Worksheet layout metadata used during post-processing.
/// </summary>
public sealed class ExcelSheetLayout(string name)
{
    /// <summary>
    /// Gets or sets the worksheet name.
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// Gets the configured column widths keyed by 1-based column index.
    /// </summary>
    public Dictionary<int, double> ColumnWidths { get; } = [];

    /// <summary>
    /// Gets the table ranges to style.
    /// </summary>
    public Collection<ExcelTableRange> TableRanges { get; } = [];

    /// <summary>
    /// Gets the explicit cell styles keyed by A1 cell reference.
    /// </summary>
    public Dictionary<string, ExcelCellStyleKind> CellStyles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the hyperlinks keyed by A1 cell reference.
    /// </summary>
    public Dictionary<string, string> Hyperlinks { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the cell comments keyed by A1 cell reference.
    /// </summary>
    public Dictionary<string, string> Comments { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Defines a worksheet table range.
/// </summary>
public sealed record ExcelTableRange(
    int HeaderRow,
    int StartColumnIndex,
    int EndColumnIndex,
    int DataStartRow,
    int DataEndRow);

/// <summary>
/// Supported workbook cell style identifiers.
/// </summary>
public enum ExcelCellStyleKind
{
    /// <summary>
    /// Default style.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Report title style.
    /// </summary>
    Title = 1,

    /// <summary>
    /// Metadata label style.
    /// </summary>
    MetadataLabel = 2,

    /// <summary>
    /// Section title style.
    /// </summary>
    SectionTitle = 3,

    /// <summary>
    /// Table header style.
    /// </summary>
    Header = 4,

    /// <summary>
    /// Table body style.
    /// </summary>
    Body = 5,

    /// <summary>
    /// Positive status style.
    /// </summary>
    StatusPositive = 6,

    /// <summary>
    /// Warning status style.
    /// </summary>
    StatusWarning = 7,

    /// <summary>
    /// Negative status style.
    /// </summary>
    StatusNegative = 8,

    /// <summary>
    /// Hyperlink style.
    /// </summary>
    Hyperlink = 9,

    /// <summary>
    /// Bold hyperlink style.
    /// </summary>
    HyperlinkBold = 10,

    /// <summary>
    /// Required-actions alert style.
    /// </summary>
    AlertRequiredActions = 11,

    /// <summary>
    /// Breaking-changes alert style.
    /// </summary>
    AlertBreakingChanges = 12,

    /// <summary>
    /// Dependency alert style.
    /// </summary>
    AlertDependency = 13,

    /// <summary>
    /// Alert details style.
    /// </summary>
    AlertDetails = 14,

    /// <summary>
    /// Muted text style.
    /// </summary>
    Muted = 15,
}
