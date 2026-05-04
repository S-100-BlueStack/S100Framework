namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level <c>calculate</c> request.
/// </summary>
public sealed record CalculateRequest
{
    /// <summary>
    /// Gets the SQL WHERE clause used to select records to update.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1=1</c>, which updates all records matched by the service and user permissions.
    /// </remarks>
    public string Where { get; init; } = "1=1";

    /// <summary>
    /// Gets the field calculation expressions to apply.
    /// </summary>
    public IReadOnlyList<CalculateExpression> Expressions { get; init; } = Array.Empty<CalculateExpression>();

    /// <summary>
    /// Gets the SQL dialect used for SQL expressions.
    /// </summary>
    /// <remarks>
    /// When omitted or set to <see cref="FeatureQuerySqlFormat.None" />, the parameter is not sent and the service
    /// uses its default behavior.
    /// </remarks>
    public FeatureQuerySqlFormat? SqlFormat { get; init; }

    /// <summary>
    /// Gets the geodatabase version to edit when the target data supports versioned editing.
    /// </summary>
    public string? GdbVersion { get; init; }

    /// <summary>
    /// Gets the branch-version editing session ID.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should return the edit moment.
    /// </summary>
    public bool ReturnEditMoment { get; init; }

    /// <summary>
    /// Validates the calculate request before it is sent.
    /// </summary>
    /// <summary>
    /// Validates the calculate request before it is sent.
    /// </summary>
    public void Validate() {
        if (Where is not null && string.IsNullOrWhiteSpace(Where)) {
            throw new InvalidOperationException("Where must not be empty when provided.");
        }

        if (Expressions is null || Expressions.Count == 0) {
            throw new InvalidOperationException("At least one calculate expression must be provided.");
        }

        foreach (var expression in Expressions) {
            if (expression is null) {
                throw new InvalidOperationException("Calculate expressions must not contain null values.");
            }

            expression.Validate();
        }

        if (GdbVersion is not null && string.IsNullOrWhiteSpace(GdbVersion)) {
            throw new InvalidOperationException("GdbVersion must not be empty when provided.");
        }

        if (SessionId is not null && string.IsNullOrWhiteSpace(SessionId)) {
            throw new InvalidOperationException("SessionId must not be empty when provided.");
        }
    }
}