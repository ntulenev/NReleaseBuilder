using System.ComponentModel.DataAnnotations;

using NReleaseBuilder.Models.Rendering;

namespace NReleaseBuilder.Configuration;

/// <summary>
/// Root application settings loaded from <c>appsettings.json</c>.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Path to the development CSV file with component images.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string DevCsvFilePath { get; init; }

    /// <summary>
    /// Path to the target CSV file with component images.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string TargetCsvFilePath { get; init; }

    /// <summary>
    /// Optional allow-list of component names loaded from CSV.
    /// When empty, all components are included.
    /// </summary>
    public IReadOnlyList<string> CsvComponentNamesFilter { get; init; } = [];

    /// <summary>
    /// Optional grouped component filters with per-group report output names.
    /// When populated, one report run is executed per group.
    /// </summary>
    public IReadOnlyList<CsvComponentGroupOptions> CsvComponentGroups { get; init; } = [];

    /// <summary>
    /// Bitbucket API options.
    /// </summary>
    [Required]
    public required BitbucketOptions Bitbucket { get; init; }

    /// <summary>
    /// Jira API options.
    /// </summary>
    [Required]
    public required JiraOptions Jira { get; init; }

    /// <summary>
    /// PDF report output options.
    /// </summary>
    [Required]
    public PdfOptions Pdf { get; init; } = new();

    /// <summary>
    /// Excel report output options.
    /// </summary>
    [Required]
    public ExcelOptions Excel { get; init; } = new();

    /// <summary>
    /// Builds report run definitions from grouped component settings.
    /// </summary>
    /// <returns>Report runs to execute.</returns>
    public IReadOnlyList<ReportRunDefinition> BuildReportRuns()
    {
        if (CsvComponentGroups.Count == 0)
        {
            return [new ReportRunDefinition(null, null, null, null)];
        }

        return
        [
            .. CsvComponentGroups.Select(static group =>
                new ReportRunDefinition(
                    string.IsNullOrWhiteSpace(group.Name) ? null : group.Name.Trim(),
                    group.ComponentNames,
                    group.PdfOutputPath,
                    group.ExcelOutputPath)),
        ];
    }
}
