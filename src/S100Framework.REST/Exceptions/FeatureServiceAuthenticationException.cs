using System.Net;

namespace S100Framework.REST.Exceptions;

public sealed class FeatureServiceAuthenticationException : InvalidOperationException
{
    public FeatureServiceAuthenticationException(
        string message,
        Uri? endpointUri = null,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException) {
        EndpointUri = endpointUri;
        StatusCode = statusCode;
    }

    public Uri? EndpointUri { get; }

    public HttpStatusCode? StatusCode { get; }
}