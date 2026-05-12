using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriSynchronizeReplicaResponseDto
{
    [JsonPropertyName("replicaID")]
    public string? ReplicaId { get; init; }

    [JsonPropertyName("replicaName")]
    public string? ReplicaName { get; init; }

    [JsonPropertyName("transportType")]
    public string? TransportType { get; init; }

    [JsonPropertyName("responseType")]
    public string? ResponseType { get; init; }

    [JsonPropertyName("replicaServerGen")]
    public JsonElement? ReplicaServerGen { get; init; }

    [JsonPropertyName("layerServerGens")]
    public List<EsriSynchronizeReplicaLayerServerGenDto?>? LayerServerGens { get; init; }

    [JsonPropertyName("resultUrl")]
    public string? ResultUrl { get; init; }

    [JsonPropertyName("URL")]
    public string? Url { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("submissionTime")]
    public JsonElement? SubmissionTime { get; init; }

    [JsonPropertyName("lastUpdatedTime")]
    public JsonElement? LastUpdatedTime { get; init; }
}

internal sealed class EsriSynchronizeReplicaJobStatusDto
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("replicaName")]
    public string? ReplicaName { get; init; }

    [JsonPropertyName("responseType")]
    public string? ResponseType { get; init; }

    [JsonPropertyName("transportType")]
    public string? TransportType { get; init; }

    [JsonPropertyName("resultUrl")]
    public string? ResultUrl { get; init; }

    [JsonPropertyName("submissionTime")]
    public JsonElement? SubmissionTime { get; init; }

    [JsonPropertyName("lastUpdatedTime")]
    public JsonElement? LastUpdatedTime { get; init; }
}

internal sealed class EsriSynchronizeReplicaLayerServerGenDto
{
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }

    [JsonPropertyName("serverGen")]
    public JsonElement? ServerGen { get; init; }
}