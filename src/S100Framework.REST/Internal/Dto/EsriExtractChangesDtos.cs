using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriExtractChangesResponseDto
{
    [JsonPropertyName("layerServerGens")]
    public List<EsriLayerServerGenDto>? LayerServerGens { get; init; }

    [JsonPropertyName("edits")]
    public List<EsriExtractChangesLayerEditsDto>? Edits { get; init; }
}

internal sealed class EsriLayerServerGenDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("serverGen")]
    public long ServerGen { get; init; }

    [JsonPropertyName("minServerGen")]
    public long? MinServerGen { get; init; }
}

internal sealed class EsriExtractChangesLayerEditsDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("objectIds")]
    public EsriExtractChangesObjectIdsDto? ObjectIds { get; init; }

    [JsonPropertyName("features")]
    public EsriExtractChangesFeaturesDto? Features { get; init; }

    [JsonPropertyName("fieldUpdates")]
    public List<JsonElement>? FieldUpdates { get; init; }

    [JsonPropertyName("hasGeometryUpdates")]
    public bool? HasGeometryUpdates { get; init; }
}

internal sealed class EsriExtractChangesObjectIdsDto
{
    [JsonPropertyName("adds")]
    public List<JsonElement>? Adds { get; init; }

    [JsonPropertyName("updates")]
    public List<JsonElement>? Updates { get; init; }

    [JsonPropertyName("deletes")]
    public List<JsonElement>? Deletes { get; init; }
}

internal sealed class EsriExtractChangesFeaturesDto
{
    [JsonPropertyName("adds")]
    public List<EsriFeatureDto>? Adds { get; init; }

    [JsonPropertyName("updates")]
    public List<EsriFeatureDto>? Updates { get; init; }

    [JsonPropertyName("deletes")]
    public List<EsriFeatureDto>? Deletes { get; init; }

    [JsonPropertyName("deleteIds")]
    public List<JsonElement>? DeleteIds { get; init; }
}