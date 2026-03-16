using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Csv;
using NReleaseBuilder.Models;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Tests.Csv;

public class CsvComponentReaderTests
{
    [Fact(DisplayName = "CsvComponentReader can be created.")]
    [Trait("Category", "Unit")]
    public void CsvComponentReaderCanBeCreated()
    {
        // Arrange
        var settings = CreateSettings("components.csv");
        var optionsValueReadCount = 0;
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock
            .Setup(x => x.Value)
            .Callback(() => optionsValueReadCount++)
            .Returns(settings);

        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new CsvComponentReader(optionsMock.Object, renderer));

        // Assert
        exception.Should().BeNull();
        optionsValueReadCount.Should().Be(1);
    }

    [Fact(DisplayName = "CsvComponentReader cant be created with null options.")]
    [Trait("Category", "Unit")]
    public void CsvComponentReaderCantBeCreatedWithNullOptions()
    {
        // Arrange
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new CsvComponentReader(null!, renderer);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact(DisplayName = "CsvComponentReader cant be created with null renderer.")]
    [Trait("Category", "Unit")]
    public void CsvComponentReaderCantBeCreatedWithNullRenderer()
    {
        // Arrange
        var settings = CreateSettings("components.csv");
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        // Act
        Action action = () => _ = new CsvComponentReader(optionsMock.Object, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("renderer");
    }

    [Fact(DisplayName = "CsvComponentReader cant be created with null settings value.")]
    [Trait("Category", "Unit")]
    public void CsvComponentReaderCantBeCreatedWithNullSettingsValue()
    {
        // Arrange
        var optionsValueReadCount = 0;
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock
            .Setup(x => x.Value)
            .Callback(() => optionsValueReadCount++)
            .Returns((AppSettings)null!);

        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new CsvComponentReader(optionsMock.Object, renderer);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
        optionsValueReadCount.Should().Be(1);
    }

    [Fact(DisplayName = "CsvComponentReader cant be created with empty csv path.")]
    [Trait("Category", "Unit")]
    public void CsvComponentReaderCantBeCreatedWithEmptyCsvPath()
    {
        // Arrange
        var settings = CreateSettings(" ");
        var optionsValueReadCount = 0;
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock
            .Setup(x => x.Value)
            .Callback(() => optionsValueReadCount++)
            .Returns(settings);

        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new CsvComponentReader(optionsMock.Object, renderer);

        // Assert
        action.Should().Throw<ArgumentException>();
        optionsValueReadCount.Should().Be(1);
    }

    [Fact(DisplayName = "CsvComponentReader Read returns parsed distinct rows sorted by component.")]
    [Trait("Category", "Integration")]
    public void ReadReturnsParsedDistinctRowsSortedByComponent()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile(
            "container,image\n"
            + "worker,registry:5000/org/repo-worker:2.0.0@sha256:abc\n"
            + "api,registry:5000/org/repo-api:1.2.3\n"
            + "api,registry:5000/org/repo-api:1.2.3\n"
            + "jobs,registry:5000/org/repo-jobs\n"
            + ",registry:5000/org/repo-empty:1.0.0\n"
            + "gateway, \n");

        var settings = CreateSettings(csvFile.Path);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;
        var sut = new CsvComponentReader(optionsMock.Object, renderer);

        // Act
        var rows = sut.Read();

        // Assert
        rows.Should().NotBeNull();
        rows.Should().Equal(
            new ComponentRow(new ComponentName("api"), new RepositoryName("repo-api"), new VersionLabel("1.2.3")),
            new ComponentRow(new ComponentName("worker"), new RepositoryName("repo-worker"), new VersionLabel("2.0.0")));
    }

    [Fact(DisplayName = "CsvComponentReader Read applies component filter case-insensitively.")]
    [Trait("Category", "Integration")]
    public void ReadAppliesComponentFilterCaseInsensitively()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile(
            "container,image\n"
            + "api,registry/org/repo-api:1.0.0\n"
            + "worker,registry/org/repo-worker:1.0.0\n"
            + "gateway,registry/org/repo-gateway:1.0.0\n");

        var settings = CreateSettings(csvFile.Path, ["  API  ", " ", "GATEWAY"]);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;
        var sut = new CsvComponentReader(optionsMock.Object, renderer);

        // Act
        var rows = sut.Read();

        // Assert
        rows.Should().NotBeNull();
        rows.Should().Equal(
            new ComponentRow(new ComponentName("api"), new RepositoryName("repo-api"), new VersionLabel("1.0.0")),
            new ComponentRow(new ComponentName("gateway"), new RepositoryName("repo-gateway"), new VersionLabel("1.0.0")));
    }

    [Fact(DisplayName = "CsvComponentReader Read merges target and dev rows and marks dev-only rows as unreleased.")]
    [Trait("Category", "Integration")]
    public void ReadMergesTargetAndDevRowsAndMarksDevOnlyRowsAsUnreleased()
    {
        // Arrange
        using var targetCsvFile = CreateTempCsvFile(
            "container,image\n"
            + "api,registry/org/repo-api:1.0.0\n"
            + "worker,registry/org/repo-worker:2.0.0\n");
        using var devCsvFile = CreateTempCsvFile(
            "container,image\n"
            + "api,registry/org/repo-api:1.1.0\n"
            + "gateway,registry/org/repo-gateway:3.0.0\n");

        var settings = CreateSettings(
            csvFilePath: string.Empty,
            targetCsvFilePath: targetCsvFile.Path,
            devCsvFilePath: devCsvFile.Path);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;
        var sut = new CsvComponentReader(optionsMock.Object, renderer);

        // Act
        var rows = sut.Read();

        // Assert
        rows.Should().NotBeNull();
        rows.Should().Equal(
            new ComponentRow(new ComponentName("api"), new RepositoryName("repo-api"), new VersionLabel("1.0.0")),
            new ComponentRow(new ComponentName("gateway"), new RepositoryName("repo-gateway"), new VersionLabel("3.0.0"), isReleased: false),
            new ComponentRow(new ComponentName("worker"), new RepositoryName("repo-worker"), new VersionLabel("2.0.0")));
    }

    [Fact(DisplayName = "CsvComponentReader ReadSourceSnapshot returns unfiltered source component sets.")]
    [Trait("Category", "Integration")]
    public void ReadSourceSnapshotReturnsUnfilteredSourceComponentSets()
    {
        // Arrange
        using var targetCsvFile = CreateTempCsvFile(
            "container,image\n"
            + "service2,registry/org/repo-service2:1.0.0\n"
            + "service1,registry/org/repo-service1:1.0.0\n");
        using var devCsvFile = CreateTempCsvFile(
            "container,image\n"
            + "service1,registry/org/repo-service1:1.1.0\n"
            + "service3,registry/org/repo-service3:2.0.0\n");

        var settings = CreateSettings(
            csvFilePath: string.Empty,
            componentNamesFilter: ["service1"],
            targetCsvFilePath: targetCsvFile.Path,
            devCsvFilePath: devCsvFile.Path);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;
        var sut = new CsvComponentReader(optionsMock.Object, renderer);

        // Act
        var snapshot = sut.ReadSourceSnapshot();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.DevComponents.Should().Equal(
            new ComponentName("service1"),
            new ComponentName("service3"));
        snapshot.TargetComponents.Should().Equal(
            new ComponentName("service1"),
            new ComponentName("service2"));
    }

    [Fact(DisplayName = "CsvComponentReader Read returns null and prints error when csv is empty.")]
    [Trait("Category", "Integration")]
    public void ReadReturnsNullAndPrintsErrorWhenCsvIsEmpty()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile(string.Empty);

        var settings = CreateSettings(csvFile.Path);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var printErrorCount = 0;
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.PrintError(It.Is<ErrorMessage>(message =>
                message.Value == "Failed to parse CSV: CSV file is empty.")))
            .Callback<ErrorMessage>(message => printErrorCount++);

        var sut = new CsvComponentReader(optionsMock.Object, rendererMock.Object);

        // Act
        var rows = sut.Read();

        // Assert
        rows.Should().BeNull();
        printErrorCount.Should().Be(1);
    }

    [Fact(DisplayName = "CsvComponentReader Read returns null and prints error when headers are missing.")]
    [Trait("Category", "Integration")]
    public void ReadReturnsNullAndPrintsErrorWhenHeadersAreMissing()
    {
        // Arrange
        using var csvFile = CreateTempCsvFile(
            "container,repository\n"
            + "api,repo-api:1.0.0\n");

        var settings = CreateSettings(csvFile.Path);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var printErrorCount = 0;
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.PrintError(It.Is<ErrorMessage>(message =>
                message.Value == "Failed to parse CSV: CSV must contain 'container' and 'image' columns.")))
            .Callback<ErrorMessage>(message => printErrorCount++);

        var sut = new CsvComponentReader(optionsMock.Object, rendererMock.Object);

        // Act
        var rows = sut.Read();

        // Assert
        rows.Should().BeNull();
        printErrorCount.Should().Be(1);
    }

    [Fact(DisplayName = "CsvComponentReader Read returns null and prints error when file is missing.")]
    [Trait("Category", "Integration")]
    public void ReadReturnsNullAndPrintsErrorWhenFileIsMissing()
    {
        // Arrange
        var missingFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

        var settings = CreateSettings(missingFilePath);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var printErrorCount = 0;
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.PrintError(It.Is<ErrorMessage>(message =>
                message.Value.StartsWith("Failed to parse CSV:", StringComparison.Ordinal)
                && message.Value.Contains(Path.GetFileName(missingFilePath), StringComparison.OrdinalIgnoreCase))))
            .Callback<ErrorMessage>(message => printErrorCount++);

        var sut = new CsvComponentReader(optionsMock.Object, rendererMock.Object);

        // Act
        var rows = sut.Read();

        // Assert
        rows.Should().BeNull();
        printErrorCount.Should().Be(1);
    }

    private static AppSettings CreateSettings(
        string csvFilePath,
        IReadOnlyList<string>? componentNamesFilter = null,
        string? targetCsvFilePath = null,
        string? devCsvFilePath = null) =>
        new()
        {
            TargetCsvFilePath = targetCsvFilePath ?? csvFilePath,
            DevCsvFilePath = devCsvFilePath ?? csvFilePath,
            CsvComponentNamesFilter = componentNamesFilter ?? [],
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
                Enabled = true,
                OutputPath = "report.pdf",
            },
        };

    private static TempCsvFile CreateTempCsvFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return new TempCsvFile(path);
    }

    private sealed class TempCsvFile(string path) : IDisposable
    {
        public string Path { get; } = path;

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
