namespace NReleaseBuilder.Abstractions.Application;

/// <summary>
/// Application workflow for component release checks.
/// </summary>
public interface IVersionCheckApplication
{
    /// <summary>
    /// Runs the application flow.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Process exit code.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken);
}
