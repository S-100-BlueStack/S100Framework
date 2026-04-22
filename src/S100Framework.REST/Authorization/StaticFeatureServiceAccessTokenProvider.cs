using S100Framework.REST.Abstractions;

namespace S100Framework.REST.Authorization;

/// <summary>
/// Returns a pre-configured access token without performing refresh or acquisition logic.
/// </summary>
/// <remarks>
/// This provider is useful when the consuming application is responsible for obtaining
/// and rotating the token outside the library.
/// </remarks>
public sealed class StaticFeatureServiceAccessTokenProvider : IFeatureServiceAccessTokenProvider
{
    private readonly FeatureServiceAccessToken _accessToken;

    /// <summary>
    /// Initializes the provider with a token value that should be treated as effectively
    /// non-expiring within the library.
    /// </summary>
    /// <param name="token">
    /// The raw access token value.
    /// </param>
    public StaticFeatureServiceAccessTokenProvider(string token)
        : this(new FeatureServiceAccessToken(token, DateTimeOffset.MaxValue)) { }

    /// <summary>
    /// Initializes the provider with a token value and explicit expiration timestamp.
    /// </summary>
    /// <param name="accessToken">
    /// The configured access token.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accessToken" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the configured token value is empty or whitespace.
    /// </exception>
    public StaticFeatureServiceAccessTokenProvider(FeatureServiceAccessToken accessToken) {
        ArgumentNullException.ThrowIfNull(accessToken);

        if (string.IsNullOrWhiteSpace(accessToken.Token)) {
            throw new ArgumentException(
                "The configured access token must not be empty.",
                nameof(accessToken));
        }

        _accessToken = accessToken;
    }

    /// <summary>
    /// Returns the configured access token.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the operation.
    /// </param>
    /// <returns>
    /// The configured access token.
    /// </returns>
    public ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
        CancellationToken cancellationToken = default) {
        return ValueTask.FromResult(_accessToken);
    }
}