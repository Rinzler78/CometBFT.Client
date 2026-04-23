using System.Text.Json.Serialization;

namespace CometBFT.Client.WebSocket.Json;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for all CometBFT WebSocket wire types.
/// Eliminates reflection on every hot serialization/deserialization path.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(WsEnvelope))]
[JsonSerializable(typeof(WsProviderErrorEnvelope))]
[JsonSerializable(typeof(WsProviderErrorPayload))]
[JsonSerializable(typeof(WsSubscribeRequest))]
[JsonSerializable(typeof(WsUnsubscribeAllRequest))]
internal sealed partial class CometBftWebSocketJsonContext : JsonSerializerContext
{
}
