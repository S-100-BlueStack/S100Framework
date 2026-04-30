namespace S100Framework.REST.Models;

/// <summary>
/// Controls shared behavior for layer-level feature edit convenience operations.
/// </summary>
public sealed record FeatureEditOptions
{
    /// <summary>
    /// Gets a value indicating whether the server should roll back the full request if one edit operation fails.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true" />.
    /// </remarks>
    public bool RollbackOnFailure { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the request should use global IDs where supported.
    /// </summary>
    public bool UseGlobalIds { get; init; }
}