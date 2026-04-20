namespace S100Framework.REST.Authorization;

/// <summary>
/// Describes how the ArcGIS token service should bind the generated token to the caller.
/// </summary>
public enum ArcGisServerTokenClientType
{
    /// <summary>
    /// Binds the token to a referer URL.
    /// </summary>
    Referer = 0,

    /// <summary>
    /// Binds the token to a specific IP address.
    /// </summary>
    Ip = 1,

    /// <summary>
    /// Binds the token to the IP address of the current request.
    /// </summary>
    RequestIp = 2
}