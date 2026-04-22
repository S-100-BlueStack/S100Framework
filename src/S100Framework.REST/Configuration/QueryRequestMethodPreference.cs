namespace S100Framework.REST.Configuration;

/// <summary>
/// Defines how the client should choose the HTTP method for query operations.
/// </summary>
public enum QueryRequestMethodPreference
{
    /// <summary>
    /// Chooses GET or POST automatically based on the configured URL length threshold.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Always uses HTTP GET for query operations.
    /// </summary>
    Get = 1,

    /// <summary>
    /// Always uses HTTP POST for query operations.
    /// </summary>
    Post = 2
}