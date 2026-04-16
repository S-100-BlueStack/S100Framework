using System.Net.Http.Headers;
using S100Framework.REST.Abstractions;

namespace S100Framework.REST.Authorization;

public sealed class BearerTokenFeatureServiceRequestAuthorizer : IFeatureServiceRequestAuthorizer
{
    private readonly Func<CancellationToken, ValueTask<string>> _tokenFactory;

    public BearerTokenFeatureServiceRequestAuthorizer(
        Func<CancellationToken, ValueTask<string>> tokenFactory) {
        _tokenFactory = tokenFactory ?? throw new ArgumentNullException(nameof(tokenFactory));
    }

    public BearerTokenFeatureServiceRequestAuthorizer(
        IFeatureServiceAccessTokenProvider tokenProvider)
        : this(CreateTokenFactory(tokenProvider)) {
    }

    public async ValueTask ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default) {
        var token = await _tokenFactory(cancellationToken);

        if (string.IsNullOrWhiteSpace(token)) {
            throw new InvalidOperationException("The token factory returned an empty token.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static Func<CancellationToken, ValueTask<string>> CreateTokenFactory(
        IFeatureServiceAccessTokenProvider tokenProvider) {
        ArgumentNullException.ThrowIfNull(tokenProvider);

        return async cancellationToken => {
            var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(accessToken.Token)) {
                throw new InvalidOperationException(
                    "The access token provider returned an empty token.");
            }

            return accessToken.Token;
        };
    }
}