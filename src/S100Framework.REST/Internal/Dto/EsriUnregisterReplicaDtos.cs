using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriUnregisterReplicaResponseDto
{
    [JsonPropertyName("success")]
    public bool? Success { get; init; }
}