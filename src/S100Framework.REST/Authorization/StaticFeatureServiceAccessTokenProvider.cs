using S100Framework.REST.Abstractions;

namespace S100Framework.REST.Authorization;

public sealed class StaticFeatureServiceAccessTokenProvider : IFeatureServiceAccessTokenProvider
{
    private readonly FeatureServiceAccessToken _accessToken;

    public StaticFeatureServiceAccessTokenProvider(string token)
        : this(new FeatureServiceAccessToken(token, DateTimeOffset.MaxValue)) {
    }

    public StaticFeatureServiceAccessTokenProvider(FeatureServiceAccessToken accessToken) {
        ArgumentNullException.ThrowIfNull(accessToken);

        if (string.IsNullOrWhiteSpace(accessToken.Token)) {
            throw new ArgumentException(
                "The configured access token must not be empty.",
                nameof(accessToken));
        }

        _accessToken = accessToken;
    }

    public ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
        CancellationToken cancellationToken = default) {
        return ValueTask.FromResult(_accessToken);
    }
}