using S100Framework.REST.Authorization;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Provides access tokens for secured Feature Service requests.
/// </summary>
/// <remarks>
/// Implementations are responsible for acquiring, caching, refreshing, or exchanging
/// tokens as needed for the target environment.
/// </remarks>
public interface IFeatureServiceAccessTokenProvider
{
    /// <summary>
    /// Gets an access token that can be used to authorize outgoing requests.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the token acquisition.
    /// </param>
    /// <returns>
    /// A token value together with its UTC expiration timestamp.
    /// </returns>
    ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
        CancellationToken cancellationToken = default);
}