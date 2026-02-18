using System.Text.Json.Serialization;

namespace Transport.Serialization;

/// <summary>
/// Source-generated JSON context for UDP wire-format serialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UdpWireMessage))]
internal sealed partial class UdpWireJsonSerializerContext : JsonSerializerContext
{
}
