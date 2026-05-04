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
/// <param name="SupportsQueryDataElements">
/// Indicates whether the service advertises support for the <c>queryDataElements</c> operation.
/// </param>
/// <param name="SupportsQueryContingentValues">
/// Indicates whether the service advertises support for the <c>queryContingentValues</c> operation.
/// </param>
/// <param name="SupportsRelationshipsResource">
/// Indicates whether the service advertises support for the service-level <c>relationships</c> resource.
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
    bool SupportsQueryDomains = false,
    bool SupportsQueryDataElements = false,
    bool SupportsQueryContingentValues = false,
    bool SupportsRelationshipsResource = false)
{
    /// <summary>
    /// Gets the formats advertised for the <c>queryContingentValues</c> operation.
    /// </summary>
    /// <remarks>
    /// ArcGIS services typically advertise values such as <c>JSON</c> and <c>PBF</c>.
    /// An empty collection means the service did not advertise supported formats.
    /// </remarks>
    public IReadOnlyList<string> SupportedContingentValuesFormats { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the contingent values JSON capability version advertised by the service.
    /// </summary>
    /// <remarks>
    /// Hosted feature services advertise <c>2</c> when they support the newer contingent values JSON format.
    /// </remarks>
    public int? ContingentValuesJsonVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service advertises support for contingent values JSON.
    /// </summary>
    public bool SupportsContingentValuesJson => ContingentValuesJsonVersion.HasValue;
}