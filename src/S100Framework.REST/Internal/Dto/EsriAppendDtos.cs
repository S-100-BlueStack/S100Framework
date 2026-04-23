using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriAppendSubmissionDto
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; init; }

    [JsonPropertyName("itemId")]
    public string? ItemId { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}

internal sealed class EsriAppendJobStatusDto
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("layerName")]
    public string? LayerName { get; init; }

    [JsonPropertyName("recordCount")]
    public long? RecordCount { get; init; }

    [JsonPropertyName("submissionTime")]
    public long? SubmissionTime { get; init; }

    [JsonPropertyName("lastUpdatedTime")]
    public long? LastUpdatedTime { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}