namespace S100Framework.REST.Models;

public sealed record FeatureServiceCapabilities(
    bool SupportsQuery,
    bool SupportsCreate,
    bool SupportsUpdate,
    bool SupportsDelete,
    bool SupportsEditing,
    bool SupportsUploads,
    bool SupportsSync,
    bool SupportsChangeTracking,
    bool SyncEnabled,
    bool SupportsAsyncApplyEdits);