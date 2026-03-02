using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace NReleaseBuilder.Configuration;

/// <summary>
/// Configuration settings for Excel report rendering.
/// </summary>
public sealed class ExcelOptions
{
    /// <summary>
    /// Enables Excel report generation.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Output file path for generated Excel report.
    /// Relative paths are resolved against the current working directory.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string OutputPath { get; init; } = "nreleasebuilder-report.xlsx";

    /// <summary>
    /// Resolves output path to an absolute file system path.
    /// </summary>
    /// <returns>Absolute output path.</returns>
    public string ResolveOutputPath()
    {
        var candidatePath = string.IsNullOrWhiteSpace(OutputPath)
            ? "nreleasebuilder-report.xlsx"
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
