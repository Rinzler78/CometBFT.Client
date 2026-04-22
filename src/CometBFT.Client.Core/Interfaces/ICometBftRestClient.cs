using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Aggregate interface for the CometBFT REST/JSON-RPC 2.0 client,
/// parameterized over the block, transaction result, and validator types.
/// </summary>
/// <typeparam name="TBlock">The block type. Must inherit <see cref="BlockBase"/>.</typeparam>
/// <typeparam name="TTxResult">The transaction result type. Must inherit <see cref="TxResultBase"/>.</typeparam>
/// <typeparam name="TValidator">The validator type. Must inherit <see cref="Validator"/>.</typeparam>
/// <remarks>
/// Use <see cref="ICometBftRestClient"/> (non-generic) to work with the default
/// <see cref="Block"/>, <see cref="TxResult"/>, and <see cref="Validator"/> types.
/// Consumers can declare a narrower interface that inherits this one to add application-layer operations
/// without redefining the entire service surface:
/// <code>
/// public interface ICosmosRestClient : ICometBftRestClient&lt;CosmosBlock, CosmosTxResult, CosmosValidator&gt; { }
/// </code>
/// </remarks>
public interface ICometBftRestClient<TBlock, TTxResult, TValidator>
    : IHealthService,
      IStatusService,
      IBlockService<TBlock>,
      ITxService<TTxResult>,
      IValidatorService<TValidator>,
      IAbciService,
      IUnsafeService
    where TBlock : BlockBase
    where TTxResult : TxResultBase
    where TValidator : Validator
{
}

/// <summary>
/// Aggregate interface for the CometBFT REST/JSON-RPC 2.0 client,
/// using the default <see cref="Block"/>, <see cref="TxResult"/>, and <see cref="Validator"/> types.
/// </summary>
public interface ICometBftRestClient : ICometBftRestClient<Block, TxResult, Validator> { }
