namespace S100Framework.REST.Models;

/// <summary>
/// Defines per-layer filtering options for a <c>createReplica</c> request.
/// </summary>
public sealed record CreateReplicaLayerQuery
{
    /// <summary>
    /// Gets the SQL where clause used to filter the layer, when specified.
    /// </summary>
    public string? Where { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request geometry should be used for this layer.
    /// </summary>
    public bool? UseGeometry { get; init; }

    /// <summary>
    /// Gets a value indicating whether related records should be included for this layer.
    /// </summary>
    public bool? IncludeRelated { get; init; }

    /// <summary>
    /// Gets the layer query option to apply.
    /// </summary>
    public CreateReplicaLayerQueryOption QueryOption { get; init; } = CreateReplicaLayerQueryOption.Default;

    /// <summary>
    /// Validates the layer query configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration is internally inconsistent.
    /// </exception>
    public void Validate() {
        if (!Enum.IsDefined(QueryOption)) {
            throw new InvalidOperationException("QueryOption must be a supported createReplica layer query option.");
        }

        if (Where is not null && string.IsNullOrWhiteSpace(Where)) {
            throw new InvalidOperationException("Layer query Where must not be empty or whitespace when provided.");
        }
    }
}