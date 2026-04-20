using S100Framework.REST.Authorization;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Provides access tokens for secured Feature Service requests.
/// </summary>
public interface IFeatureServiceAccessTokenProvider
{
    /// <summary>
    /// Gets an access token that can be used to authorize outgoing requests.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the token acquisition.
    /// </param>
    /// <returns>
    /// A token value and its expiration timestamp.
    /// </returns>
    ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
        CancellationToken cancellationToken = default);
}