using FluentAssertions;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Presentation.Excel;

namespace NReleaseBuilder.Tests.Presentation.Excel;

public class ExcelReportFileStoreTests
{
    [Fact(DisplayName = "ExcelReportFileStore CreateWorkbookStream throws when sheets are null.")]
    [Trait("Category", "Unit")]
    public void CreateWorkbookStreamThrowsWhenSheetsAreNull()
    {
        // Arrange
        var sut = new ExcelReportFileStore(Options.Create(CreateSettings("report.xlsx")));

        // Act
        Action action = () => sut.CreateWorkbookStream(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("sheets");
    }

    [Fact(DisplayName = "ExcelReportFileStore CreateWorkbookStream creates readable workbook stream.")]
    [Trait("Category", "Unit")]
    public void CreateWorkbookStreamCreatesReadableWorkbookStream()
    {
        // Arrange
        var sut = new ExcelReportFileStore(Options.Create(CreateSettings("report.xlsx")));
        IReadOnlyDictionary<string, object> sheets = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Summary"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal)
                {
                    ["C1"] = "Hello",
                },
            },
        };

        // Act
        using var stream = sut.CreateWorkbookStream(sheets);

        // Assert
        stream.Length.Should().BeGreaterThan(0);
        stream.Position.Should().Be(0);
        stream.CanRead.Should().BeTrue();
        stream.CanWrite.Should().BeTrue();
    }

    [Fact(DisplayName = "ExcelReportFileStore can be created.")]
    [Trait("Category", "Unit")]
    public void ExcelReportFileStoreCanBeCreated()
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => _ = new ExcelReportFileStore(Options.Create(CreateSettings("report.xlsx"))));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "ExcelReportFileStore can not be created with null options.")]
    [Trait("Category", "Unit")]
    public void ExcelReportFileStoreCantBeCreatedWithNullOptions()
    {
        // Arrange
        // Act
        Action action = () => _ = new ExcelReportFileStore(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact(DisplayName = "ExcelReportFileStore Save throws when content stream is null.")]
    [Trait("Category", "Unit")]
    public void SaveThrowsWhenContentStreamIsNull()
    {
        // Arrange
        var sut = new ExcelReportFileStore(Options.Create(CreateSettings("report.xlsx")));

        // Act
        Action action = () => sut.Save(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("contentStream");
    }

    [Fact(DisplayName = "ExcelReportFileStore Save creates output directory and writes bytes.")]
    [Trait("Category", "Integration")]
    public void SaveCreatesOutputDirectoryAndWritesBytes()
    {
        // Arrange
        using var tempDirectory = new TempDirectory();
        var configuredOutputPath = Path.Combine(tempDirectory.Path, "nested", "report.xlsx");
        var expectedContent = new byte[] { 1, 2, 3, 4, 5 };
        var settings = CreateSettings(configuredOutputPath);
        var expectedOutputPath = settings.Excel.ResolveOutputPath();

        var sut = new ExcelReportFileStore(Options.Create(settings));
        using var stream = new MemoryStream(expectedContent);

        // Act
        var actualOutputPath = sut.Save(stream);

        // Assert
        actualOutputPath.Should().Be(expectedOutputPath);
        File.Exists(expectedOutputPath).Should().BeTrue();
        File.ReadAllBytes(expectedOutputPath).Should().Equal(expectedContent);
        Directory.Exists(Path.GetDirectoryName(expectedOutputPath)).Should().BeTrue();
    }

    [Fact(DisplayName = "ExcelReportFileStore Save overwrites an existing file.")]
    [Trait("Category", "Integration")]
    public void SaveOverwritesAnExistingFile()
    {
        // Arrange
        using var tempDirectory = new TempDirectory();
        var configuredOutputPath = Path.Combine(tempDirectory.Path, "report.xlsx");
        var settings = CreateSettings(configuredOutputPath);
        var expectedOutputPath = settings.Excel.ResolveOutputPath();
        File.WriteAllBytes(expectedOutputPath, [9, 9, 9]);

        var sut = new ExcelReportFileStore(Options.Create(settings));
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var actualOutputPath = sut.Save(stream);

        // Assert
        actualOutputPath.Should().Be(expectedOutputPath);
        File.ReadAllBytes(expectedOutputPath).Should().Equal([1, 2, 3]);
    }

    private static AppSettings CreateSettings(string excelOutputPath) =>
        new()
        {
            CsvFilePath = "components.csv",
            CsvComponentNamesFilter = [],
            Bitbucket = new BitbucketOptions
            {
                BaseUrl = new Uri("https://bitbucket.example.test/"),
                Workspace = "workspace",
                ProjectNames = ["PROJ"],
                AuthEmail = "bot@example.test",
                AuthApiToken = "token",
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
                Email = "jira@example.test",
                ApiToken = "token",
            },
            Pdf = new PdfOptions
            {
                Enabled = false,
                OutputPath = "report.pdf",
            },
            Excel = new ExcelOptions
            {
                Enabled = true,
                OutputPath = excelOutputPath,
            },
        };

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
