using System.ComponentModel;

namespace S100Framework.REST.Models;

public sealed record FeatureLayerSchema
{
    public FeatureLayerSchema(
        int layerId,
        string name,
        string? geometryType,
        int? srid,
        bool hasZ,
        bool hasM,
        int? maxRecordCount,
        string? objectIdFieldName,
        IReadOnlyList<FeatureField> fields,
        FeatureLayerCapabilities capabilities,
        IReadOnlyList<FeatureRelationshipInfo> relationships) {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(relationships);

        LayerId = layerId;
        Name = name;
        GeometryType = geometryType;
        Srid = srid;
        HasZ = hasZ;
        HasM = hasM;
        MaxRecordCount = maxRecordCount;
        ObjectIdFieldName = objectIdFieldName;
        Fields = fields;
        Capabilities = capabilities;
        Relationships = relationships;
    }

    [Obsolete(
        "Use the constructor overload without supportsPagination. Pagination support is exposed through Capabilities.SupportsPagination.")]
    public FeatureLayerSchema(
        int layerId,
        string name,
        string? geometryType,
        int? srid,
        bool hasZ,
        bool hasM,
        bool supportsPagination,
        int? maxRecordCount,
        string? objectIdFieldName,
        IReadOnlyList<FeatureField> fields,
        FeatureLayerCapabilities capabilities,
        IReadOnlyList<FeatureRelationshipInfo> relationships)
        : this(
            layerId,
            name,
            geometryType,
            srid,
            hasZ,
            hasM,
            maxRecordCount,
            objectIdFieldName,
            fields,
            capabilities,
            relationships) {
        if (supportsPagination != capabilities.SupportsPagination) {
            throw new InvalidOperationException(
                "The legacy supportsPagination value must match Capabilities.SupportsPagination.");
        }
    }

    public int LayerId { get; init; }

    public string Name { get; init; }

    public string? GeometryType { get; init; }

    public int? Srid { get; init; }

    public bool HasZ { get; init; }

    public bool HasM { get; init; }

    [Obsolete("Use Capabilities.SupportsPagination instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool SupportsPagination => Capabilities.SupportsPagination;

    public int? MaxRecordCount { get; init; }

    public string? ObjectIdFieldName { get; init; }

    public IReadOnlyList<FeatureField> Fields { get; init; }

    public FeatureLayerCapabilities Capabilities { get; init; }

    public IReadOnlyList<FeatureRelationshipInfo> Relationships { get; init; }
}