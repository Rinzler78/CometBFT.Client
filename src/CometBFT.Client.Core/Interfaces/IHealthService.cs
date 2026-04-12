namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides node health checking operations for a CometBFT node.
/// </summary>
public interface IHealthService
{
    /// <summary>
    /// Checks whether the CometBFT node is healthy and reachable.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <c>true</c> if the node responded with a healthy status; <c>false</c> otherwise.
    /// </returns>
    Task<bool> GetHealthAsync(CancellationToken cancellationToken = default);
}
