using System.Globalization;

using FluentAssertions;

using NReleaseBuilder.Configuration;

namespace NReleaseBuilder.Tests.Configuration;

public class PdfOptionsTests
{
    [Fact(DisplayName = "PdfOptions ResolveOutputPath resolves relative path to absolute path.")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathResolvesRelativePathToAbsolutePath()
    {
        // Arrange
        var options = new PdfOptions
        {
            OutputPath = Path.Combine("reports", "result.pdf"),
        };
        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expected = Path.GetFullPath(
            Path.Combine("reports", $"result_{dateSuffix}.pdf"),
            Directory.GetCurrentDirectory());

        // Act
        var result = options.ResolveOutputPath();

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "PdfOptions ResolveOutputPath trims output path before resolving.")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathTrimsOutputPathBeforeResolving()
    {
        // Arrange
        var options = new PdfOptions
        {
            OutputPath = "  report.pdf  ",
        };
        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expected = Path.GetFullPath($"report_{dateSuffix}.pdf", Directory.GetCurrentDirectory());

        // Act
        var result = options.ResolveOutputPath();

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "PdfOptions ResolveOutputPath uses default path when output path is empty.")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathUsesDefaultPathWhenOutputPathIsEmpty()
    {
        // Arrange
        var options = new PdfOptions
        {
            OutputPath = " ",
        };
        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expected = Path.GetFullPath($"nreleasebuilder-report_{dateSuffix}.pdf", Directory.GetCurrentDirectory());

        // Act
        var result = options.ResolveOutputPath();

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "PdfOptions ResolveOutputPath returns absolute path for rooted input.")]
    [Trait("Category", "Unit")]
    public void ResolveOutputPathReturnsAbsolutePathForRootedInput()
    {
        // Arrange
        var rootedPath = Path.Combine(Path.GetTempPath(), "nrb-report.pdf");
        var options = new PdfOptions
        {
            OutputPath = rootedPath,
        };
        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expected = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(rootedPath)!, $"nrb-report_{dateSuffix}.pdf"));

        // Act
        var result = options.ResolveOutputPath();

        // Assert
        result.Should().Be(expected);
    }
}
