using FluentAssertions;

using NReleaseBuilder.Presentation.Pdf;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NReleaseBuilder.Tests.Presentation.Pdf;

public class PdfReportFileStoreTests
{
    [Fact(DisplayName = "PdfReportFileStore can be created.")]
    [Trait("Category", "Unit")]
    public void PdfReportFileStoreCanBeCreated()
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => _ = new PdfReportFileStore());

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "PdfReportFileStore Save throws when output path is empty.")]
    [Trait("Category", "Unit")]
    public void SaveThrowsWhenOutputPathIsEmpty()
    {
        // Arrange
        var sut = new PdfReportFileStore();
        var document = CreateDocument("hello");

        // Act
        Action action = () => sut.Save(" ", document);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "PdfReportFileStore Save throws when document is null.")]
    [Trait("Category", "Unit")]
    public void SaveThrowsWhenDocumentIsNull()
    {
        // Arrange
        var sut = new PdfReportFileStore();

        // Act
        Action action = () => sut.Save("report.pdf", null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("document");
    }

    [Fact(DisplayName = "PdfReportFileStore Save creates output directory and writes PDF bytes.")]
    [Trait("Category", "Integration")]
    public void SaveCreatesOutputDirectoryAndWritesPdfBytes()
    {
        // Arrange
        QuestPDF.Settings.License = LicenseType.Community;

        using var tempDirectory = new TempDirectory();
        var outputPath = Path.Combine(tempDirectory.Path, "nested", "report.pdf");

        var sut = new PdfReportFileStore();
        var document = CreateDocument("Generated report");

        // Act
        sut.Save(outputPath, document);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var fileInfo = new FileInfo(outputPath);
        fileInfo.Length.Should().BeGreaterThan(0);
        Directory.Exists(Path.GetDirectoryName(outputPath)).Should().BeTrue();
    }

    [Fact(DisplayName = "PdfReportFileStore Save overwrites an existing file.")]
    [Trait("Category", "Integration")]
    public void SaveOverwritesAnExistingFile()
    {
        // Arrange
        QuestPDF.Settings.License = LicenseType.Community;

        using var tempDirectory = new TempDirectory();
        var outputPath = Path.Combine(tempDirectory.Path, "report.pdf");

        File.WriteAllText(outputPath, "old content");
        var existingLength = new FileInfo(outputPath).Length;

        var sut = new PdfReportFileStore();
        var document = CreateDocument("New content");

        // Act
        sut.Save(outputPath, document);

        // Assert
        File.Exists(outputPath).Should().BeTrue();
        var fileInfo = new FileInfo(outputPath);
        fileInfo.Length.Should().BeGreaterThan(0);
        fileInfo.Length.Should().NotBe(existingLength);
    }

    private static Document CreateDocument(string text)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            _ = container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10);
                _ = page.Content().Text(text);
            });
        });
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nrb-tests-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
