using System.Globalization;

using FluentAssertions;

using NReleaseBuilder.Configuration;

namespace NReleaseBuilder.Tests.Configuration;

public class ExcelOptionsTests
{
    [Fact(DisplayName = "ExcelOptions ResolveOutputPath resolves relative path to absolute path.")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathResolvesRelativePathToAbsolutePath()
    {
        // Arrange
        var options = new ExcelOptions
        {
            OutputPath = Path.Combine("reports", "result.xlsx"),
        };
        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expected = Path.GetFullPath(
            Path.Combine("reports", $"result_{dateSuffix}.xlsx"),
            Directory.GetCurrentDirectory());

        // Act
        var result = options.ResolveOutputPath();

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "ExcelOptions ResolveOutputPath trims output path before resolving.")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathTrimsOutputPathBeforeResolving()
    {
        // Arrange
        var options = new ExcelOptions
        {
            OutputPath = "  report.xlsx  ",
        };
        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expected = Path.GetFullPath($"report_{dateSuffix}.xlsx", Directory.GetCurrentDirectory());

        // Act
        var result = options.ResolveOutputPath();

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "ExcelOptions ResolveOutputPath uses default path when output path is empty.")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathUsesDefaultPathWhenOutputPathIsEmpty()
    {
        // Arrange
        var options = new ExcelOptions
        {
            OutputPath = " ",
        };
        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expected = Path.GetFullPath($"nreleasebuilder-report_{dateSuffix}.xlsx", Directory.GetCurrentDirectory());

        // Act
        var result = options.ResolveOutputPath();

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "ExcelOptions ResolveOutputPath returns absolute path for rooted input.")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathReturnsAbsolutePathForRootedInput()
    {
        // Arrange
        var rootedPath = Path.Combine(Path.GetTempPath(), "nrb-report.xlsx");
        var options = new ExcelOptions
        {
            OutputPath = rootedPath,
        };
        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expected = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(rootedPath)!, $"nrb-report_{dateSuffix}.xlsx"));

        // Act
        var result = options.ResolveOutputPath();

        // Assert
        result.Should().Be(expected);
    }
}
