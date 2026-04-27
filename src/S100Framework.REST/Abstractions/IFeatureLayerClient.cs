using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines the public contract for working with a single feature layer or table.
/// </summary>
/// <remarks>
/// Operation-specific members are declared in partial interface files to keep each Feature Service operation group
/// easier to review and maintain without changing the public interface type.
/// </remarks>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Gets the schema metadata for the current layer or table.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The layer schema metadata.
    /// </returns>
    Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default);
}