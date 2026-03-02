using System.Globalization;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Models.Rendering;

namespace NReleaseBuilder.Presentation.Excel;

/// <summary>
/// OpenXML-based workbook formatter.
/// </summary>
public sealed class OpenXmlWorkbookFormatter : IWorkbookFormatter
{
    /// <inheritdoc />
    public void Format(Stream workbookStream, IReadOnlyDictionary<string, ExcelSheetLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(workbookStream);
        ArgumentNullException.ThrowIfNull(layouts);

        workbookStream.Position = 0;
        using (var spreadsheet = SpreadsheetDocument.Open(workbookStream, true))
        {
            var workbookPart = spreadsheet.WorkbookPart
                ?? throw new InvalidOperationException("Workbook part is missing.");
            var workbook = workbookPart.Workbook
                ?? throw new InvalidOperationException("Workbook is missing.");

            EnsureStylesheet(workbookPart);

            foreach (var sheet in workbook.Sheets?.OfType<Sheet>() ?? [])
            {
                if (!layouts.TryGetValue(sheet.Name?.Value ?? string.Empty, out var layout))
                {
                    continue;
                }

                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                var worksheet = worksheetPart.Worksheet
                    ?? throw new InvalidOperationException("Worksheet is missing.");
                var sheetData = worksheet.GetFirstChild<SheetData>()
                    ?? throw new InvalidOperationException("Worksheet is missing sheet data.");

                ApplyColumnWidths(worksheet, layout.ColumnWidths);
                ApplyTableStyles(sheetData, layout.TableRanges);
                ApplyCellStyles(sheetData, layout.CellStyles);
                ApplyComments(worksheetPart, layout.Comments);
                ApplyHyperlinks(worksheetPart, layout.Hyperlinks);

                worksheet.Save();
            }

            workbookPart.Workbook.Save();
        }

        workbookStream.Position = 0;
    }

    private static void EnsureStylesheet(WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);

        var stylesPart = workbookPart.WorkbookStylesPart ?? workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = CreateStylesheet();
        stylesPart.Stylesheet.Save();
    }

    private static Stylesheet CreateStylesheet()
    {
        var fonts = new Fonts(
            new Font(),
            new Font(new Bold()),
            new Font(new Bold(), new FontSize { Val = 16D }),
            new Font(new Bold(), new FontSize { Val = 12D }),
            new Font(new Color { Rgb = LINK_COLOR_HEX }, new Underline()),
            new Font(new Bold(), new Color { Rgb = LINK_COLOR_HEX }, new Underline()),
            new Font(new Bold(), new Color { Rgb = ToRgb("15803d") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("ea580c") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("b91c1c") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("ffaf00") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("ff0000") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("00afff") }),
            new Font(new Color { Rgb = MUTED_HEX }),
            new Font(new Bold(), new Color { Rgb = ToRgb("374151") }))
        {
            Count = 14U,
            KnownFonts = true,
        };

        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(
                new ForegroundColor { Rgb = HEADER_FILL_HEX },
                new BackgroundColor { Indexed = 64U })
            { PatternType = PatternValues.Solid }),
            new Fill(new PatternFill(
                new ForegroundColor { Rgb = ALERT_DETAILS_FILL_HEX },
                new BackgroundColor { Indexed = 64U })
            { PatternType = PatternValues.Solid }))
        {
            Count = 4U,
        };

        var borders = new Borders(
            new Border(),
            new Border(
                new LeftBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = BORDER_HEX } },
                new RightBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = BORDER_HEX } },
                new TopBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = BORDER_HEX } },
                new BottomBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = BORDER_HEX } },
                new DiagonalBorder()))
        {
            Count = 2U,
        };

        var cellStyleFormats = new CellStyleFormats(new CellFormat())
        {
            Count = 1U,
        };

        var cellFormats = new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 2U, ApplyFont = true },
            new CellFormat { FontId = 13U, ApplyFont = true },
            new CellFormat { FontId = 3U, ApplyFont = true },
            CreateBorderedFormat(fontId: 1U, fillId: 2U),
            CreateBorderedFormat(),
            CreateBorderedFormat(fontId: 6U),
            CreateBorderedFormat(fontId: 7U),
            CreateBorderedFormat(fontId: 8U),
            CreateBorderedFormat(fontId: 4U),
            CreateBorderedFormat(fontId: 5U),
            CreateBorderedFormat(fontId: 9U),
            CreateBorderedFormat(fontId: 10U),
            CreateBorderedFormat(fontId: 11U),
            CreateBorderedFormat(fillId: 3U),
            CreateBorderedFormat(fontId: 12U))
        {
            Count = 16U,
        };

        return new Stylesheet(fonts, fills, borders, cellStyleFormats, cellFormats);
    }

    private static CellFormat CreateBorderedFormat(uint fontId = 0U, uint fillId = 0U) =>
        new()
        {
            FontId = fontId,
            FillId = fillId,
            BorderId = 1U,
            ApplyFont = true,
            ApplyFill = fillId != 0U,
            ApplyBorder = true,
            ApplyAlignment = true,
            Alignment = new Alignment
            {
                Vertical = VerticalAlignmentValues.Top,
                WrapText = true,
            },
        };

    private static void ApplyColumnWidths(Worksheet worksheet, Dictionary<int, double> columnWidths)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(columnWidths);

        if (columnWidths.Count == 0)
        {
            return;
        }

        worksheet.RemoveAllChildren<Columns>();

        var columns = new Columns();
        foreach (var (columnIndex, width) in columnWidths.OrderBy(static x => x.Key))
        {
            columns.Append(new Column
            {
                Min = (uint)columnIndex,
                Max = (uint)columnIndex,
                Width = width,
                CustomWidth = true,
            });
        }

        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        _ = worksheet.InsertBefore(columns, sheetData);
    }

    private static void ApplyTableStyles(SheetData sheetData, IEnumerable<ExcelTableRange> tableRanges)
    {
        ArgumentNullException.ThrowIfNull(sheetData);
        ArgumentNullException.ThrowIfNull(tableRanges);

        foreach (var range in tableRanges)
        {
            for (var columnIndex = range.StartColumnIndex; columnIndex <= range.EndColumnIndex; columnIndex++)
            {
                SetCellStyle(sheetData, columnIndex, range.HeaderRow, ExcelCellStyleKind.Header);
            }

            for (var rowIndex = range.DataStartRow; rowIndex <= range.DataEndRow; rowIndex++)
            {
                for (var columnIndex = range.StartColumnIndex; columnIndex <= range.EndColumnIndex; columnIndex++)
                {
                    SetCellStyle(sheetData, columnIndex, rowIndex, ExcelCellStyleKind.Body);
                }
            }
        }
    }

    private static void ApplyCellStyles(SheetData sheetData, IReadOnlyDictionary<string, ExcelCellStyleKind> cellStyles)
    {
        ArgumentNullException.ThrowIfNull(sheetData);
        ArgumentNullException.ThrowIfNull(cellStyles);

        foreach (var (cellReference, styleKind) in cellStyles)
        {
            var (columnIndex, rowIndex) = ParseCellReference(cellReference);
            SetCellStyle(sheetData, columnIndex, rowIndex, styleKind);
        }
    }

    private static void ApplyHyperlinks(WorksheetPart worksheetPart, Dictionary<string, string> hyperlinks)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(hyperlinks);

        if (hyperlinks.Count == 0)
        {
            return;
        }

        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidOperationException("Worksheet is missing.");
        var hyperlinksElement = worksheet.GetFirstChild<Hyperlinks>();
        if (hyperlinksElement is null)
        {
            hyperlinksElement = new Hyperlinks();
            var pageMargins = worksheet.GetFirstChild<PageMargins>();
            _ = pageMargins is not null ?
                worksheet.InsertBefore(hyperlinksElement, pageMargins) :
                worksheet.AppendChild(hyperlinksElement);
        }

        foreach (var (cellReference, targetUrl) in hyperlinks)
        {
            var relationship = worksheetPart.AddHyperlinkRelationship(new Uri(targetUrl, UriKind.Absolute), true);
            _ = hyperlinksElement.AppendChild(new Hyperlink
            {
                Reference = cellReference,
                Id = relationship.Id,
            });
        }
    }

    private static void ApplyComments(WorksheetPart worksheetPart, Dictionary<string, string> comments)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(comments);

        if (comments.Count == 0)
        {
            return;
        }

        var commentsPart = worksheetPart.WorksheetCommentsPart ?? worksheetPart.AddNewPart<WorksheetCommentsPart>();
        commentsPart.Comments ??= new Comments(
            new Authors(new Author("NReleaseBuilder")),
            new CommentList());

        var commentList = commentsPart.Comments.GetFirstChild<CommentList>()
            ?? commentsPart.Comments.AppendChild(new CommentList());

        foreach (var (cellReference, commentText) in comments)
        {
            _ = commentList.AppendChild(new Comment
            {
                Reference = cellReference,
                AuthorId = 0U,
                CommentText = new CommentText(new Run(new Text(commentText))),
            });
        }
    }

    private static void SetCellStyle(SheetData sheetData, int columnIndex, int rowIndex, ExcelCellStyleKind styleKind)
    {
        var cell = GetOrCreateCell(sheetData, columnIndex, rowIndex);
        cell.StyleIndex = (uint)styleKind;
    }

    private static Cell GetOrCreateCell(SheetData sheetData, int columnIndex, int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(sheetData);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowIndex, 1);

        var row = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == (uint)rowIndex);
        if (row is null)
        {
            row = new Row { RowIndex = (uint)rowIndex };

            var nextRow = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value > (uint)rowIndex);
            _ = nextRow is null ? sheetData.AppendChild(row) : sheetData.InsertBefore(row, nextRow);
        }

        var cellReference = ToCellReference(columnIndex, rowIndex);
        var cell = row.Elements<Cell>().FirstOrDefault(c => string.Equals(c.CellReference?.Value, cellReference, StringComparison.OrdinalIgnoreCase));
        if (cell is not null)
        {
            return cell;
        }

        cell = new Cell
        {
            CellReference = cellReference,
            DataType = CellValues.String,
            CellValue = new CellValue(string.Empty),
        };

        var nextCell = row.Elements<Cell>()
            .FirstOrDefault(c => CompareCellReferences(c.CellReference?.Value, cellReference) > 0);

        _ = nextCell is null ? row.AppendChild(cell) : row.InsertBefore(cell, nextCell);

        return cell;
    }

    private static int CompareCellReferences(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return 1;
        }

        var (leftColumn, leftRow) = ParseCellReference(left);
        var (rightColumn, rightRow) = ParseCellReference(right);

        var rowComparison = leftRow.CompareTo(rightRow);
        return rowComparison != 0 ? rowComparison : leftColumn.CompareTo(rightColumn);
    }

    private static (int ColumnIndex, int RowIndex) ParseCellReference(string cellReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cellReference);

        var column = 0;
        var index = 0;
        while (index < cellReference.Length && char.IsLetter(cellReference[index]))
        {
            var letterValue = char.ToUpperInvariant(cellReference[index]) - 'A' + 1;
            column = (column * 26) + letterValue;
            index++;
        }

        var row = int.Parse(cellReference[index..], CultureInfo.InvariantCulture);
        return (column, row);
    }

    private static string ToCellReference(int columnIndex, int rowIndex) =>
        ToColumnName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);

    private static string ToColumnName(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, 1);

        var dividend = columnIndex;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo, CultureInfo.InvariantCulture) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string ToRgb(string value) => value.TrimStart('#').ToUpperInvariant();

    private const string LINK_COLOR_HEX = "1D4ED8";
    private const string HEADER_FILL_HEX = "F3F4F6";
    private const string ALERT_DETAILS_FILL_HEX = "F9FAFB";
    private const string BORDER_HEX = "D1D5DB";
    private const string MUTED_HEX = "6B7280";
}
