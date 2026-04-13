namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Aggregate interface for the CometBFT REST/JSON-RPC 2.0 client.
/// Inherits all service interfaces for health, status, blocks, transactions,
/// validators, ABCI operations, and unsafe node management endpoints.
/// </summary>
public interface ICometBftRestClient
    : IHealthService,
      IStatusService,
      IBlockService,
      ITxService,
      IValidatorService,
      IAbciService,
      IUnsafeService
{
}
