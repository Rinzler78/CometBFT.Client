using System.Text.Json.Serialization;

namespace CometBFT.Client.Rest.Json;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for all CometBFT REST wire types.
/// Eliminates reflection on every hot deserialization path.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(JsonRpcResponse<RpcStatusResult>))]
[JsonSerializable(typeof(JsonRpcResponse<RpcBlockIdResult>))]
[JsonSerializable(typeof(JsonRpcResponse<RpcBlockResultsResult>))]
[JsonSerializable(typeof(JsonRpcResponse<RpcValidatorsResult>))]
[JsonSerializable(typeof(JsonRpcResponse<RpcTx>))]
[JsonSerializable(typeof(JsonRpcResponse<RpcTxSearchResult>))]
[JsonSerializable(typeof(JsonRpcResponse<RpcBroadcastResult>))]
[JsonSerializable(typeof(JsonRpcResponse<RpcAbciInfoResult>))]
[JsonSerializable(typeof(JsonRpcResponse<RpcAbciQueryResult>))]
[JsonSerializable(typeof(JsonRpcBroadcastRequest))]
internal sealed partial class CometBftJsonContext : JsonSerializerContext
{
}
