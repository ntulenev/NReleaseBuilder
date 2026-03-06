using System.ComponentModel.DataAnnotations;

namespace NReleaseBuilder.Configuration;

/// <summary>
/// Component group definition for per-group report generation.
/// </summary>
public sealed class CsvComponentGroupOptions
{
    /// <summary>
    /// Human-readable group name.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Component names that belong to this group.
    /// </summary>
    public IReadOnlyList<string> ComponentNames { get; init; } = [];

    /// <summary>
    /// Optional PDF output path override for this group.
    /// </summary>
    public string? PdfOutputPath { get; init; }

    /// <summary>
    /// Optional Excel output path override for this group.
    /// </summary>
    public string? ExcelOutputPath { get; init; }
}
