namespace S100Framework.REST.Abstractions;

public interface IFeatureServiceRequestAuthorizer
{
    ValueTask ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}