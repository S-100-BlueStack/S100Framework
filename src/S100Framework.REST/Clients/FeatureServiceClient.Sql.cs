using System.Globalization;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides SQL validation operations for feature layer endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal async Task<ValidateSqlResult> ValidateSqlAsync(
        int layerId,
        ValidateSqlRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        request.Validate();

        var endpointUri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                UriUtility.AppendPath(_serviceUri, layerId.ToString(CultureInfo.InvariantCulture)),
                "validateSQL"),
            new Dictionary<string, string?> {
                ["f"] = "json",
                ["sql"] = request.Sql,
                ["sqlType"] = MapValidateSqlType(request.SqlType)
            });

        var dto = await GetAsync<EsriValidateSqlResponseDto>(endpointUri, cancellationToken);

        if (!dto.IsValidSql.HasValue) {
            throw new FeatureServiceException(
                "The server returned a validateSQL response without an isValidSQL value.",
                endpointUri);
        }

        return new ValidateSqlResult(
            dto.IsValidSql.Value,
            MapValidationErrors(dto.ValidationErrors));
    }

    private static IReadOnlyList<ValidateSqlValidationError> MapValidationErrors(
        IEnumerable<EsriValidateSqlErrorDto?>? errors) {
        if (errors is null) {
            return Array.Empty<ValidateSqlValidationError>();
        }

        return errors
            .Where(static error => error is not null)
            .Select(static error => new ValidateSqlValidationError(
                error!.ErrorCode,
                error.Description))
            .ToArray();
    }

    private static string MapValidateSqlType(ValidateSqlType value) {
        return value switch {
            ValidateSqlType.Where => "where",
            ValidateSqlType.Expression => "expression",
            ValidateSqlType.Statement => "statement",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported SQL validation type.")
        };
    }
}