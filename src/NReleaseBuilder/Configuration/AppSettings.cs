using System.ComponentModel.DataAnnotations;

namespace NReleaseBuilder.Configuration;

/// <summary>
/// Root application settings loaded from <c>appsettings.json</c>.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Path to source CSV file with component images.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string CsvFilePath { get; init; }

    /// <summary>
    /// Optional allow-list of component names loaded from CSV.
    /// When empty, all components are included.
    /// </summary>
    public IReadOnlyList<string> CsvComponentNamesFilter { get; init; } = [];

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
}
