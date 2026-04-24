namespace S100Framework.REST.Models;

/// <summary>
/// Describes the core capabilities exposed by a feature service.
/// </summary>
/// <param name="SupportsQuery">
/// Indicates whether query operations are supported.
/// </param>
/// <param name="SupportsCreate">
/// Indicates whether create operations are supported.
/// </param>
/// <param name="SupportsUpdate">
/// Indicates whether update operations are supported.
/// </param>
/// <param name="SupportsDelete">
/// Indicates whether delete operations are supported.
/// </param>
/// <param name="SupportsEditing">
/// Indicates whether general editing is supported.
/// </param>
/// <param name="SupportsUploads">
/// Indicates whether upload operations are supported.
/// </param>
/// <param name="SupportsSync">
/// Indicates whether sync operations are supported.
/// </param>
/// <param name="SupportsChangeTracking">
/// Indicates whether change tracking is supported.
/// </param>
/// <param name="SyncEnabled">
/// Indicates whether sync is currently enabled for the service.
/// </param>
/// <param name="SupportsAsyncApplyEdits">
/// Indicates whether asynchronous service-level <c>applyEdits</c> is supported.
/// </param>
/// <param name="SupportsAppend">
/// Indicates whether the service advertises support for the <c>append</c> operation.
/// </param>
/// <param name="SupportsQueryDomains">
/// Indicates whether the service advertises support for the <c>queryDomains</c> operation.
/// </param>
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
    bool SupportsAsyncApplyEdits,
    bool SupportsAppend = false,
    bool SupportsQueryDomains = false);