namespace S100Framework.REST.Abstractions;

/// <summary>
/// Applies authorization details to outgoing Feature Service HTTP requests.
/// </summary>
/// <remarks>
/// Implementations can add headers, query parameters, or other request-specific
/// authorization data before the request is sent.
/// </remarks>
public interface IFeatureServiceRequestAuthorizer
{
    /// <summary>
    /// Applies authorization to the specified outgoing request.
    /// </summary>
    /// <param name="request">
    /// The HTTP request to authorize.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the authorization operation.
    /// </param>
    /// <returns>
    /// A task that completes when authorization has been applied.
    /// </returns>
    ValueTask ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}