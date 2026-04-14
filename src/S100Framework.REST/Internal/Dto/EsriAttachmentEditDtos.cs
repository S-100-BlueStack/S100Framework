using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriDeleteAttachmentsResponseDto
{
    [JsonPropertyName("deleteAttachmentResults")]
    public List<EsriAttachmentEditResultDto>? DeleteAttachmentResults { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}

internal sealed class EsriAttachmentEditResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("objectId")]
    public long? ObjectId { get; init; }

    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    [JsonPropertyName("error")]
    public EsriEditErrorDto? Error { get; init; }
}

internal sealed class EsriAddAttachmentResponseDto
{
    [JsonPropertyName("addAttachmentResult")]
    public EsriAttachmentEditResultDto? AddAttachmentResult { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}

internal sealed class EsriUpdateAttachmentResponseDto
{
    [JsonPropertyName("updateAttachmentResults")]
    public List<EsriAttachmentEditResultDto>? UpdateAttachmentResults { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}