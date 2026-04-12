using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides validator set querying operations for a CometBFT chain.
/// </summary>
public interface IValidatorService
{
    /// <summary>
    /// Retrieves the validator set at the specified block height.
    /// Returns the latest validator set when <paramref name="height"/> is <c>null</c>.
    /// </summary>
    /// <param name="height">The block height, or <c>null</c> for the latest validator set.</param>
    /// <param name="page">The 1-based page number for pagination.</param>
    /// <param name="perPage">The number of validators per page (max 100).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of <see cref="Validator"/> objects in the set.</returns>
    Task<IReadOnlyList<Validator>> GetValidatorsAsync(
        long? height = null,
        int? page = null,
        int? perPage = null,
        CancellationToken cancellationToken = default);
}
