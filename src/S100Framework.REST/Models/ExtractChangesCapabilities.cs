namespace S100Framework.REST.Models;

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