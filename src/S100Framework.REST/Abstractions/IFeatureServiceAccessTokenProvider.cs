using S100Framework.REST.Authorization;

namespace S100Framework.REST.Abstractions;

public interface IFeatureServiceAccessTokenProvider
{
    ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
        CancellationToken cancellationToken = default);
}