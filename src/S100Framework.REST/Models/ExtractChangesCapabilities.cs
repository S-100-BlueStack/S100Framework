namespace S100Framework.REST.Models;

/// <summary>
/// Describes the <c>extractChanges</c>-specific capabilities exposed by a feature service.
/// </summary>
/// <param name="SupportsReturnIdsOnly">
/// Indicates whether ID-only change responses are supported.
/// </param>
/// <param name="SupportsReturnExtentOnly">
/// Indicates whether extent-only responses are supported.
/// </param>
/// <param name="SupportsReturnAttachments">
/// Indicates whether attachment changes can be returned.
/// </param>
/// <param name="SupportsLayerQueries">
/// Indicates whether per-layer query filters are supported.
/// </param>
/// <param name="SupportsGeometry">
/// Indicates whether spatial filters are supported.
/// </param>
/// <param name="SupportsReturnFeature">
/// Indicates whether full feature payloads can be returned.
/// </param>
/// <param name="SupportsFieldsToCompare">
/// Indicates whether field comparison configuration is supported.
/// </param>
/// <param name="SupportsServerGens">
/// Indicates whether server generation parameters are supported.
/// </param>
/// <param name="SupportsReturnHasGeometryUpdates">
/// Indicates whether update responses can include whether geometry changed.
/// </param>
public sealed record ExtractChangesCapabilities(
    bool SupportsReturnIdsOnly,
    bool SupportsReturnExtentOnly,
    bool SupportsReturnAttachments,
    bool SupportsLayerQueries,
    bool SupportsGeometry,
    bool SupportsReturnFeature,
    bool SupportsFieldsToCompare,
    bool SupportsServerGens,
    bool SupportsReturnHasGeometryUpdates);