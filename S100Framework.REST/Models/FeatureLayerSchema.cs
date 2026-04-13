namespace S100Framework.REST.Models;

public sealed record FeatureLayerSchema(
    int LayerId,
    string Name,
    string? GeometryType,
    int? Srid,
    bool HasZ,
    bool HasM,
    bool SupportsPagination,
    int? MaxRecordCount,
    string? ObjectIdFieldName,
    IReadOnlyList<FeatureField> Fields);