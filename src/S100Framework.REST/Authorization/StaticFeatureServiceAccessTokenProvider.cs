using S100Framework.REST.Abstractions;

namespace S100Framework.REST.Authorization;

/// <summary>
/// Returns a pre-configured access token without performing refresh or acquisition logic.
/// </summary>
public sealed class StaticFeatureServiceAccessTokenProvider : IFeatureServiceAccessTokenProvider
{
    private readonly FeatureServiceAccessToken _accessToken;

    /// <summary>
    /// Initializes the provider with a token value that does not expire within the library.
    /// </summary>
    /// <param name="token">The raw access token value.</param>
    public StaticFeatureServiceAccessTokenProvider(string token)
        : this(new FeatureServiceAccessToken(token, DateTimeOffset.MaxValue)) {
    }

    /// <summary>
    /// Initializes the provider with a token value and explicit expiration timestamp.
    /// </summary>
    /// <param name="accessToken">The configured access token.</param>
    public StaticFeatureServiceAccessTokenProvider(FeatureServiceAccessToken accessToken) {
        ArgumentNullException.ThrowIfNull(accessToken);

        if (string.IsNullOrWhiteSpace(accessToken.Token)) {
            throw new ArgumentException(
                "The configured access token must not be empty.",
                nameof(accessToken));
        }

        _accessToken = accessToken;
    }

    /// <inheritdoc />
    public ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
        CancellationToken cancellationToken = default) {
        return ValueTask.FromResult(_accessToken);
    }
}