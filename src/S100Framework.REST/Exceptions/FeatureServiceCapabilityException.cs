namespace S100Framework.REST.Exceptions;

public sealed class FeatureServiceCapabilityException : InvalidOperationException
{
    public FeatureServiceCapabilityException(string message)
        : base(message) {
    }
}