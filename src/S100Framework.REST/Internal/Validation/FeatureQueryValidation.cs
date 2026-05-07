using System.Text.Json;
using S100Framework.REST.Models;

namespace S100Framework.REST.Internal.Validation;

internal static class FeatureQueryValidation
{
    internal static void ValidateCommon(FeatureQuery query) {
        ArgumentNullException.ThrowIfNull(query);

        if (query.OrderBy is not null && string.IsNullOrWhiteSpace(query.OrderBy)) {
            throw new InvalidOperationException("OrderBy must not be empty when provided.");
        }

        if (query.GdbVersion is not null && string.IsNullOrWhiteSpace(query.GdbVersion)) {
            throw new InvalidOperationException("GdbVersion must not be empty when provided.");
        }

        if (query.DefaultSrid is <= 0) {
            throw new InvalidOperationException("DefaultSrid must be greater than zero when provided.");
        }

        if (query.DatumTransformationWkid is <= 0) {
            throw new InvalidOperationException("DatumTransformationWkid must be greater than zero when provided.");
        }

        if (query.DatumTransformationWkid.HasValue && query.DatumTransformationJson is not null) {
            throw new InvalidOperationException(
                "DatumTransformationWkid and DatumTransformationJson cannot both be specified.");
        }

        ValidateJsonObject(query.DatumTransformationJson, nameof(query.DatumTransformationJson));
        ValidateJsonObject(query.QuantizationParametersJson, nameof(query.QuantizationParametersJson));

        if (query.ResultType.HasValue && !Enum.IsDefined(query.ResultType.Value)) {
            throw new InvalidOperationException("ResultType must be a supported query result type.");
        }

        if (query.MultipatchOption.HasValue && !Enum.IsDefined(query.MultipatchOption.Value)) {
            throw new InvalidOperationException("MultipatchOption must be a supported multipatch option.");
        }

        if (query.SqlFormat.HasValue && !Enum.IsDefined(query.SqlFormat.Value)) {
            throw new InvalidOperationException("SqlFormat must be a supported SQL format.");
        }

        if (query.MultipatchOption.HasValue && !query.ReturnGeometry) {
            throw new InvalidOperationException("MultipatchOption requires ReturnGeometry to be true.");
        }

        ValidateUniqueIds(query.UniqueIds);

        if (query.TimeInstant.HasValue && query.TimeExtent is not null) {
            throw new InvalidOperationException("TimeInstant and TimeExtent cannot both be specified.");
        }

        if (query.TimeExtent is not null) {
            if (!query.TimeExtent.Start.HasValue && !query.TimeExtent.End.HasValue) {
                throw new InvalidOperationException("TimeExtent must specify at least one bound.");
            }

            if (query.TimeExtent.Start.HasValue &&
                query.TimeExtent.End.HasValue &&
                query.TimeExtent.Start.Value > query.TimeExtent.End.Value) {
                throw new InvalidOperationException(
                    "TimeExtent.Start must be less than or equal to TimeExtent.End when both are provided.");
            }
        }
    }

    internal static void ValidateFullText(FeatureQuery query) {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FullText is null) {
            return;
        }

        if (query.FullText.Count == 0) {
            throw new InvalidOperationException("FullText must contain at least one expression when provided.");
        }

        foreach (var expression in query.FullText) {
            if (expression is null) {
                throw new InvalidOperationException("FullText must not contain null expressions.");
            }

            var hasSqlExpression = !string.IsNullOrWhiteSpace(expression.SqlExpression);
            var hasStructuredExpression =
                expression.OnFields is { Count: > 0 } &&
                !string.IsNullOrWhiteSpace(expression.SearchTerm);

            if (hasSqlExpression == hasStructuredExpression) {
                throw new InvalidOperationException(
                    "Each FullText expression must specify either SqlExpression or OnFields/SearchTerm, but not both.");
            }

            if (hasStructuredExpression &&
                expression.OnFields!.Any(static field => string.IsNullOrWhiteSpace(field))) {
                throw new InvalidOperationException("FullText OnFields must not contain empty field names.");
            }

            if (expression.SearchType.HasValue && !Enum.IsDefined(expression.SearchType.Value)) {
                throw new InvalidOperationException("FullText SearchType must be a supported full text search type.");
            }

            if (expression.Operator.HasValue && !Enum.IsDefined(expression.Operator.Value)) {
                throw new InvalidOperationException("FullText Operator must be a supported full text operator.");
            }

            if (expression.SearchOperator.HasValue && !Enum.IsDefined(expression.SearchOperator.Value)) {
                throw new InvalidOperationException("FullText SearchOperator must be a supported full text search operator.");
            }
        }
    }

    internal static void ValidateProjection(FeatureQuery query) {
        ArgumentNullException.ThrowIfNull(query);

        if (query.OutSrid is <= 0) {
            throw new InvalidOperationException("OutSrid must be greater than zero when provided.");
        }
    }

    internal static void ValidateGeometryOptions(FeatureQuery query) {
        ArgumentNullException.ThrowIfNull(query);

        if (query.GeometryPrecision is < 0) {
            throw new InvalidOperationException("GeometryPrecision must be greater than or equal to zero when provided.");
        }

        if (query.MaxAllowableOffset.HasValue &&
    (double.IsNaN(query.MaxAllowableOffset.Value) ||
     double.IsInfinity(query.MaxAllowableOffset.Value))) {
            throw new InvalidOperationException("MaxAllowableOffset must be a finite value when provided.");
        }

        if (query.MaxAllowableOffset is < 0) {
            throw new InvalidOperationException("MaxAllowableOffset must be greater than or equal to zero when provided.");
        }
    }

    internal static void ValidateOutFields(FeatureQuery query) {
        ArgumentNullException.ThrowIfNull(query);

        if (query.OutFields?.Any(static field => string.IsNullOrWhiteSpace(field)) == true) {
            throw new InvalidOperationException("OutFields must not contain null, empty, or whitespace-only values.");
        }
    }

    private static void ValidateJsonObject(string? json, string propertyName) {
        if (json is null) {
            return;
        }

        if (string.IsNullOrWhiteSpace(json)) {
            throw new InvalidOperationException($"{propertyName} must not be empty when provided.");
        }

        try {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException($"{propertyName} must be a JSON object.");
            }
        }
        catch (JsonException exception) {
            throw new InvalidOperationException($"{propertyName} must contain valid JSON.", exception);
        }
    }

    private static void ValidateUniqueIds(IReadOnlyList<FeatureUniqueId>? uniqueIds) {
        if (uniqueIds is { Count: 0 }) {
            throw new InvalidOperationException("UniqueIds must contain at least one identifier when provided.");
        }

        if (uniqueIds is null) {
            return;
        }

        foreach (var uniqueId in uniqueIds) {
            if (uniqueId is null) {
                throw new InvalidOperationException("UniqueIds must not contain null values.");
            }

            if (uniqueId.Components is not { Count: > 0 }) {
                throw new InvalidOperationException("Each UniqueIds entry must contain at least one component.");
            }

            if (uniqueId.Components.Any(string.IsNullOrWhiteSpace)) {
                throw new InvalidOperationException("UniqueIds components must not be empty.");
            }
        }
    }
}