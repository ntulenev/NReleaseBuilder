using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class BitbucketProgressCallbacksTests
{
    [Fact(DisplayName = "BitbucketProgressCallbacks can assign and invoke callbacks.")]
    [Trait("Category", "Unit")]
    public void CanAssignAndInvokeCallbacks()
    {
        // Arrange
        var startedCount = 0;
        var completedCount = 0;
        var processedCount = 0;
        var detectedCount = 0;
        var callbacks = new BitbucketProgressCallbacks
        {
            RepositoryStarted = _ => startedCount++,
            RepositoryCompleted = _ => completedCount++,
            CommitProcessed = _ => processedCount++,
            CommitTotalDetected = (_, _) => detectedCount++,
        };

        // Act
        callbacks.RepositoryStarted?.Invoke("repo");
        callbacks.CommitTotalDetected?.Invoke("repo", 2);
        callbacks.CommitProcessed?.Invoke("repo");
        callbacks.RepositoryCompleted?.Invoke("repo");

        // Assert
        startedCount.Should().Be(1);
        detectedCount.Should().Be(1);
        processedCount.Should().Be(1);
        completedCount.Should().Be(1);
    }

    [Fact(DisplayName = "BitbucketProgressCallbacks callbacks are null by default.")]
    [Trait("Category", "Unit")]
    public void CallbacksAreNullByDefault()
    {
        // Arrange
        var callbacks = new BitbucketProgressCallbacks();

        // Act
        var repositoryStarted = callbacks.RepositoryStarted;
        var commitTotalDetected = callbacks.CommitTotalDetected;
        var commitProcessed = callbacks.CommitProcessed;
        var repositoryCompleted = callbacks.RepositoryCompleted;

        // Assert
        repositoryStarted.Should().BeNull();
        commitTotalDetected.Should().BeNull();
        commitProcessed.Should().BeNull();
        repositoryCompleted.Should().BeNull();
    }
}
