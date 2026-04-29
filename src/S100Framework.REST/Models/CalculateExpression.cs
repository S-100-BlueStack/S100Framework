namespace S100Framework.REST.Models;

/// <summary>
/// Represents one field calculation expression in a layer-level <c>calculate</c> request.
/// </summary>
public sealed record CalculateExpression
{
    /// <summary>
    /// Gets the target field to update.
    /// </summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the expression uses a scalar value or SQL expression.
    /// </summary>
    public CalculateExpressionKind Kind { get; init; } = CalculateExpressionKind.Value;

    /// <summary>
    /// Gets the scalar value to assign when <see cref="Kind" /> is <see cref="CalculateExpressionKind.Value" />.
    /// </summary>
    /// <remarks>
    /// Set this to <see langword="null" /> to send a JSON <c>null</c> value.
    /// </remarks>
    public object? Value { get; init; }

    /// <summary>
    /// Gets the SQL expression to evaluate when <see cref="Kind" /> is <see cref="CalculateExpressionKind.SqlExpression" />.
    /// </summary>
    public string? SqlExpression { get; init; }

    /// <summary>
    /// Creates a scalar value calculation expression.
    /// </summary>
    /// <param name="field">
    /// The target field to update.
    /// </param>
    /// <param name="value">
    /// The scalar value to assign. Use <see langword="null" /> to send a JSON <c>null</c> value.
    /// </param>
    /// <returns>
    /// The created calculation expression.
    /// </returns>
    public static CalculateExpression ForValue(string field, object? value) {
        return new CalculateExpression {
            Field = field,
            Kind = CalculateExpressionKind.Value,
            Value = value
        };
    }

    /// <summary>
    /// Creates a scalar calculation expression that assigns a JSON <c>null</c> value.
    /// </summary>
    /// <param name="field">
    /// The target field to update.
    /// </param>
    /// <returns>
    /// The created calculation expression.
    /// </returns>

    public static CalculateExpression ForNull(string field) {
        return ForValue(field, value: null);
    }

    /// <summary>
    /// Creates a SQL-based calculation expression.
    /// </summary>
    /// <param name="field">
    /// The target field to update.
    /// </param>
    /// <param name="sqlExpression">
    /// The SQL expression evaluated by the service.
    /// </param>
    /// <returns>
    /// The created calculation expression.
    /// </returns>
    public static CalculateExpression ForSqlExpression(string field, string sqlExpression) {
        return new CalculateExpression {
            Field = field,
            Kind = CalculateExpressionKind.SqlExpression,
            SqlExpression = sqlExpression
        };
    }

    /// <summary>
    /// Validates the calculation expression before it is sent.
    /// </summary>
    public void Validate() {
        if (string.IsNullOrWhiteSpace(Field)) {
            throw new InvalidOperationException("CalculateExpression.Field must be provided.");
        }

        switch (Kind) {
            case CalculateExpressionKind.Value:
                if (SqlExpression is not null) {
                    throw new InvalidOperationException(
                        "CalculateExpression.SqlExpression can only be used when Kind is SqlExpression.");
                }

                break;

            case CalculateExpressionKind.SqlExpression:
                if (string.IsNullOrWhiteSpace(SqlExpression)) {
                    throw new InvalidOperationException(
                        "CalculateExpression.SqlExpression must be provided when Kind is SqlExpression.");
                }

                if (Value is not null) {
                    throw new InvalidOperationException(
                        "CalculateExpression.Value can only be used when Kind is Value.");
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(Kind),
                    Kind,
                    "Unsupported calculate expression kind.");
        }
    }

    internal Dictionary<string, object?> ToJsonPayload() {
        Validate();

        var payload = new Dictionary<string, object?> {
            ["field"] = Field
        };

        if (Kind == CalculateExpressionKind.SqlExpression) {
            payload["sqlExpression"] = SqlExpression;
        }
        else {
            payload["value"] = Value;
        }

        return payload;
    }
}