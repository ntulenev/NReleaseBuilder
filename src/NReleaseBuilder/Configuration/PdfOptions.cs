using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace NReleaseBuilder.Configuration;

/// <summary>
/// Configuration settings for PDF report rendering.
/// </summary>
public sealed class PdfOptions
{
    /// <summary>
    /// Enables PDF report generation.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Output file path for generated PDF report.
    /// Relative paths are resolved against the current working directory.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string OutputPath { get; init; } = "nreleasebuilder-report.pdf";

    /// <summary>
    /// Resolves output path to an absolute file system path.
    /// </summary>
    /// <returns>Absolute output path.</returns>
    public string ResolveOutputPath()
    {
        var candidatePath = string.IsNullOrWhiteSpace(OutputPath)
            ? "nreleasebuilder-report.pdf"
            : OutputPath.Trim();

        var absolutePath = Path.IsPathRooted(candidatePath)
            ? Path.GetFullPath(candidatePath)
            : Path.GetFullPath(candidatePath, Directory.GetCurrentDirectory());

        return AppendDateSuffix(absolutePath, DateTime.Now);
    }

    private static string AppendDateSuffix(string absolutePath, DateTime currentDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var directoryPath = Path.GetDirectoryName(absolutePath);
        var extension = Path.GetExtension(absolutePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absolutePath);
        var dateSuffix = currentDate.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var datedFileName = fileNameWithoutExtension + "_" + dateSuffix + extension;

        return string.IsNullOrWhiteSpace(directoryPath)
            ? datedFileName
            : Path.Combine(directoryPath, datedFileName);
    }
}
