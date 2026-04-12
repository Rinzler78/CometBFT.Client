using System.Globalization;
using System.Text.Json.Nodes;

namespace CometBFT.Client.Rest.Internal;

internal static class RestClientHelpers
{
    internal static string BuildQueryString(params (string? Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrEmpty(p.Key) && !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key!)}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? "?" + qs : string.Empty;
    }

    internal static string NormalizeHash(string hash) =>
        hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hash : "0x" + hash;

    internal static long ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    internal static long ParseLongNode(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<string>(out var stringValue))
            {
                return ParseLong(stringValue);
            }
        }

        return 0;
    }
}
