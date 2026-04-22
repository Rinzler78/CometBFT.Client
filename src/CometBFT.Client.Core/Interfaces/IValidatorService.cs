using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides validator set querying operations for a CometBFT chain,
/// returning validators as <typeparamref name="TValidator"/>.
/// </summary>
/// <typeparam name="TValidator">
/// The validator type. Must inherit <see cref="Validator"/>.
/// Use <see cref="IValidatorService"/> (non-generic) to work with plain <see cref="Validator"/> instances.
/// </typeparam>
public interface IValidatorService<TValidator> where TValidator : Validator
{
    /// <summary>
    /// Retrieves the validator set at the specified block height.
    /// Returns the latest validator set when <paramref name="height"/> is <c>null</c>.
    /// </summary>
    /// <param name="height">The block height, or <c>null</c> for the latest validator set.</param>
    /// <param name="page">The 1-based page number for pagination.</param>
    /// <param name="perPage">The number of validators per page (max 100).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of <typeparamref name="TValidator"/> objects in the set.</returns>
    Task<IReadOnlyList<TValidator>> GetValidatorsAsync(
        long? height = null,
        int? page = null,
        int? perPage = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides validator set querying operations for a CometBFT chain,
/// returning plain <see cref="Validator"/> instances.
/// </summary>
public interface IValidatorService : IValidatorService<Validator> { }
