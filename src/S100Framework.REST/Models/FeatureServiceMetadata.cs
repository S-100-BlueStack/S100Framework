namespace S100Framework.REST.Models;

/// <summary>
/// Describes the metadata exposed by an ArcGIS Feature Service.
/// </summary>
public sealed record FeatureServiceMetadata
{
    /// <summary>
    /// Initializes the metadata model.
    /// </summary>
    /// <param name="serviceUri">The base URI of the feature service.</param>
    /// <param name="layers">The layers exposed by the service.</param>
    /// <param name="tables">The tables exposed by the service.</param>
    /// <param name="capabilityText">The raw ArcGIS capabilities string returned by the service.</param>
    /// <param name="maxRecordCount">The maximum record count configured by the service, when available.</param>
    /// <param name="capabilities">The parsed service capabilities.</param>
    /// <param name="extractChangesCapabilities">
    /// The parsed extractChanges capabilities, when the service exposes them.
    /// </param>
    public FeatureServiceMetadata(
        Uri serviceUri,
        IReadOnlyList<FeatureServiceDatasetInfo> layers,
        IReadOnlyList<FeatureServiceDatasetInfo> tables,
        string? capabilityText,
        int? maxRecordCount,
        FeatureServiceCapabilities capabilities,
        ExtractChangesCapabilities? extractChangesCapabilities) {
        ArgumentNullException.ThrowIfNull(serviceUri);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(capabilities);

        ServiceUri = serviceUri;
        Layers = layers;
        Tables = tables;
        CapabilityText = capabilityText;
        MaxRecordCount = maxRecordCount;
        Capabilities = capabilities;
        ExtractChangesCapabilities = extractChangesCapabilities;
    }

    /// <summary>
    /// Gets the base URI of the feature service.
    /// </summary>
    public Uri ServiceUri { get; init; }

    /// <summary>
    /// Gets the layers exposed by the feature service.
    /// </summary>
    public IReadOnlyList<FeatureServiceDatasetInfo> Layers { get; init; }

    /// <summary>
    /// Gets the tables exposed by the feature service.
    /// </summary>
    public IReadOnlyList<FeatureServiceDatasetInfo> Tables { get; init; }

    /// <summary>
    /// Gets the raw ArcGIS capabilities string returned by the service.
    /// </summary>
    public string? CapabilityText { get; init; }

    /// <summary>
    /// Gets the maximum record count configured by the service, when available.
    /// </summary>
    public int? MaxRecordCount { get; init; }

    /// <summary>
    /// Gets the parsed service capabilities.
    /// </summary>
    public FeatureServiceCapabilities Capabilities { get; init; }

    /// <summary>
    /// Gets the parsed extractChanges capabilities, when the service exposes them.
    /// </summary>
    public ExtractChangesCapabilities? ExtractChangesCapabilities { get; init; }
}

/// <summary>
/// Describes a layer or table exposed by an ArcGIS Feature Service.
/// </summary>
public sealed record FeatureServiceDatasetInfo
{
    /// <summary>
    /// Initializes the dataset metadata model.
    /// </summary>
    /// <param name="id">The layer or table identifier.</param>
    /// <param name="name">The layer or table name.</param>
    public FeatureServiceDatasetInfo(
        int id,
        string name) {
        ArgumentNullException.ThrowIfNull(name);

        Id = id;
        Name = name;
    }

    /// <summary>
    /// Gets the layer or table identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the layer or table name.
    /// </summary>
    public string Name { get; init; }
}