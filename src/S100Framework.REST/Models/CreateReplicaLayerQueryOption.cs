namespace S100Framework.REST.Models;

/// <summary>
/// Specifies how a layer participates in a <c>createReplica</c> request.
/// </summary>
public enum CreateReplicaLayerQueryOption
{
    /// <summary>
    /// Applies the service default behavior for the layer.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Excludes the layer rows from the replica while preserving the layer definition.
    /// </summary>
    None = 1,

    /// <summary>
    /// Applies the layer filter configured on the layer query.
    /// </summary>
    UseFilter = 2,

    /// <summary>
    /// Includes all rows for the layer.
    /// </summary>
    All = 3
}