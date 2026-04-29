using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriUploadResponseDto
{
    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    [JsonPropertyName("item")]
    public EsriUploadItemDto? Item { get; init; }
}

internal sealed class EsriUploadItemDto
{
    [JsonPropertyName("itemID")]
    public string? ItemId { get; init; }

    [JsonPropertyName("itemName")]
    public string? ItemName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("date")]
    public long? Date { get; init; }

    [JsonPropertyName("committed")]
    public bool? Committed { get; init; }
}

internal sealed class EsriUploadDeleteResponseDto
{
    [JsonPropertyName("success")]
    public bool? Success { get; init; }
}