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

    public async ValueTask ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default) {
        var token = await _tokenFactory(cancellationToken);

        if (string.IsNullOrWhiteSpace(token)) {
            throw new InvalidOperationException("The token factory returned an empty token.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}