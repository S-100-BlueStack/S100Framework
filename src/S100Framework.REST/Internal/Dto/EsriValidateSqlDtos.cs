using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriValidateSqlResponseDto
{
    [JsonPropertyName("isValidSQL")]
    public bool? IsValidSql { get; init; }

    [JsonPropertyName("validationErrors")]
    public List<EsriValidateSqlErrorDto>? ValidationErrors { get; init; }
}

internal sealed class EsriValidateSqlErrorDto
{
    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}