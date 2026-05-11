using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriFeatureServiceReplicaDto
{
    [JsonPropertyName("replicaName")]
    public string? ReplicaName { get; init; }

    [JsonPropertyName("replicaID")]
    public string? ReplicaId { get; init; }

    [JsonPropertyName("replicaVersion")]
    public string? ReplicaVersion { get; init; }

    [JsonPropertyName("lastSyncDate")]
    public JsonElement? LastSyncDate { get; init; }
}