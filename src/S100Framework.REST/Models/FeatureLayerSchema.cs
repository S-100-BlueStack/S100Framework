using System.ComponentModel;

namespace S100Framework.REST.Models;

/// <summary>
/// Describes the schema metadata exposed by a layer or table.
/// </summary>
public sealed record FeatureLayerSchema
{
    /// <summary>
    /// Initializes the schema model.
    /// </summary>
    /// <param name="layerId">
    /// The layer or table identifier.
    /// </param>
    /// <param name="name">
    /// The layer or table name.
    /// </param>
    /// <param name="geometryType">
    /// The ArcGIS geometry type identifier, when available.
    /// </param>
    /// <param name="srid">
    /// The spatial reference ID, when available.
    /// </param>
    /// <param name="hasZ">
    /// Indicates whether the layer geometry includes Z values.
    /// </param>
    /// <param name="hasM">
    /// Indicates whether the layer geometry includes M values.
    /// </param>
    /// <param name="maxRecordCount">
    /// The maximum record count configured for the layer, when available.
    /// </param>
    /// <param name="objectIdFieldName">
    /// The object ID field name, when available.
    /// </param>
    /// <param name="fields">
    /// The fields exposed by the layer or table.
    /// </param>
    /// <param name="capabilities">
    /// The parsed layer capabilities.
    /// </param>
    /// <param name="relationships">
    /// The relationships exposed by the layer, when available.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when required constructor arguments are <see langword="null" />.
    /// </exception>
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

    /// <summary>
    /// Initializes the schema model using the legacy pagination constructor shape.
    /// </summary>
    /// <param name="layerId">
    /// The layer or table identifier.
    /// </param>
    /// <param name="name">
    /// The layer or table name.
    /// </param>
    /// <param name="geometryType">
    /// The ArcGIS geometry type identifier, when available.
    /// </param>
    /// <param name="srid">
    /// The spatial reference ID, when available.
    /// </param>
    /// <param name="hasZ">
    /// Indicates whether the layer geometry includes Z values.
    /// </param>
    /// <param name="hasM">
    /// Indicates whether the layer geometry includes M values.
    /// </param>
    /// <param name="supportsPagination">
    /// The legacy pagination flag. This must match
    /// <see cref="FeatureLayerCapabilities.SupportsPagination" />.
    /// </param>
    /// <param name="maxRecordCount">
    /// The maximum record count configured for the layer, when available.
    /// </param>
    /// <param name="objectIdFieldName">
    /// The object ID field name, when available.
    /// </param>
    /// <param name="fields">
    /// The fields exposed by the layer or table.
    /// </param>
    /// <param name="capabilities">
    /// The parsed layer capabilities.
    /// </param>
    /// <param name="relationships">
    /// The relationships exposed by the layer, when available.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the legacy pagination value does not match
    /// <see cref="FeatureLayerCapabilities.SupportsPagination" />.
    /// </exception>
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

    /// <summary>
    /// Gets the layer or table identifier.
    /// </summary>
    public int LayerId { get; init; }

    /// <summary>
    /// Gets the layer or table name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the ArcGIS geometry type identifier, when available.
    /// </summary>
    public string? GeometryType { get; init; }

    /// <summary>
    /// Gets the spatial reference ID, when available.
    /// </summary>
    public int? Srid { get; init; }

    /// <summary>
    /// Gets a value indicating whether the layer geometry includes Z values.
    /// </summary>
    public bool HasZ { get; init; }

    /// <summary>
    /// Gets a value indicating whether the layer geometry includes M values.
    /// </summary>
    public bool HasM { get; init; }

    /// <summary>
    /// Gets a legacy pagination flag.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Capabilities" /> and
    /// <see cref="FeatureLayerCapabilities.SupportsPagination" /> instead.
    /// </remarks>
    [Obsolete("Use Capabilities.SupportsPagination instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool SupportsPagination => Capabilities.SupportsPagination;

    /// <summary>
    /// Gets the maximum record count configured for the layer, when available.
    /// </summary>
    public int? MaxRecordCount { get; init; }

    /// <summary>
    /// Gets the object ID field name, when available.
    /// </summary>
    public string? ObjectIdFieldName { get; init; }

    /// <summary>
    /// Gets the fields exposed by the layer or table.
    /// </summary>
    public IReadOnlyList<FeatureField> Fields { get; init; }

    /// <summary>
    /// Gets the parsed layer capabilities.
    /// </summary>
    public FeatureLayerCapabilities Capabilities { get; init; }

    /// <summary>
    /// Gets the relationships exposed by the layer, when available.
    /// </summary>
    public IReadOnlyList<FeatureRelationshipInfo> Relationships { get; init; }
}