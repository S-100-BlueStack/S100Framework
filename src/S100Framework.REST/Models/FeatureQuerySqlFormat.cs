namespace S100Framework.REST.Models;

/// <summary>
/// Defines the SQL dialect mode requested from the feature service query endpoint.
/// </summary>
public enum FeatureQuerySqlFormat
{
    /// <summary>
    /// Lets the service interpret SQL based on its configured behavior.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requests standardized SQL-92 syntax.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Requests native DBMS SQL syntax when supported by the service.
    /// </summary>
    Native = 2
}