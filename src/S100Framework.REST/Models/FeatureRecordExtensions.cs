using System.Globalization;

namespace S100Framework.REST.Models;

public static class FeatureRecordExtensions
{
    public static bool HasAttribute(this FeatureRecord feature, string attributeName) {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        return feature.Attributes.ContainsKey(attributeName);
    }

    public static object? GetRawValue(this FeatureRecord feature, string attributeName) {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        return feature.Attributes.TryGetValue(attributeName, out var value)
            ? value
            : null;
    }

    public static string? GetString(this FeatureRecord feature, string attributeName) {
        return ConvertToString(feature.GetRawValue(attributeName));
    }

    public static string GetRequiredString(this FeatureRecord feature, string attributeName) {
        var value = feature.GetString(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "string");
        }

        return value;
    }

    public static int? GetInt32(this FeatureRecord feature, string attributeName) {
        return ConvertToInt32(feature.GetRawValue(attributeName));
    }

    public static int GetRequiredInt32(this FeatureRecord feature, string attributeName) {
        var value = feature.GetInt32(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Int32");
        }

        return value.Value;
    }

    public static long? GetInt64(this FeatureRecord feature, string attributeName) {
        return ConvertToInt64(feature.GetRawValue(attributeName));
    }

    public static long GetRequiredInt64(this FeatureRecord feature, string attributeName) {
        var value = feature.GetInt64(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Int64");
        }

        return value.Value;
    }

    public static decimal? GetDecimal(this FeatureRecord feature, string attributeName) {
        return ConvertToDecimal(feature.GetRawValue(attributeName));
    }

    public static decimal GetRequiredDecimal(this FeatureRecord feature, string attributeName) {
        var value = feature.GetDecimal(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Decimal");
        }

        return value.Value;
    }

    public static double? GetDouble(this FeatureRecord feature, string attributeName) {
        return ConvertToDouble(feature.GetRawValue(attributeName));
    }

    public static double GetRequiredDouble(this FeatureRecord feature, string attributeName) {
        var value = feature.GetDouble(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Double");
        }

        return value.Value;
    }

    public static bool? GetBoolean(this FeatureRecord feature, string attributeName) {
        return ConvertToBoolean(feature.GetRawValue(attributeName));
    }

    public static bool GetRequiredBoolean(this FeatureRecord feature, string attributeName) {
        var value = feature.GetBoolean(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Boolean");
        }

        return value.Value;
    }

    private static string? ConvertToString(object? value) {
        return value switch {
            null => null,
            string stringValue => stringValue,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static int? ConvertToInt32(object? value) {
        return value switch {
            null => null,
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
            decimal decimalValue when decimalValue >= int.MinValue &&
                                      decimalValue <= int.MaxValue &&
                                      decimal.Truncate(decimalValue) == decimalValue => (int)decimalValue,
            double doubleValue when doubleValue >= int.MinValue &&
                                    doubleValue <= int.MaxValue &&
                                    Math.Abs(doubleValue % 1) < double.Epsilon => (int)doubleValue,
            string stringValue when int.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            IConvertible convertible => TryConvert(
                () => Convert.ToInt32(convertible, CultureInfo.InvariantCulture)),
            _ => null
        };
    }

    private static long? ConvertToInt64(object? value) {
        return value switch {
            null => null,
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue when decimal.Truncate(decimalValue) == decimalValue &&
                                      decimalValue >= long.MinValue &&
                                      decimalValue <= long.MaxValue => (long)decimalValue,
            double doubleValue when Math.Abs(doubleValue % 1) < double.Epsilon &&
                                    doubleValue >= long.MinValue &&
                                    doubleValue <= long.MaxValue => (long)doubleValue,
            string stringValue when long.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            IConvertible convertible => TryConvert(
                () => Convert.ToInt64(convertible, CultureInfo.InvariantCulture)),
            _ => null
        };
    }

    private static decimal? ConvertToDecimal(object? value) {
        return value switch {
            null => null,
            decimal decimalValue => decimalValue,
            long longValue => longValue,
            int intValue => intValue,
            double doubleValue when !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue) => (decimal)doubleValue,
            string stringValue when decimal.TryParse(
                stringValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            IConvertible convertible => TryConvert(
                () => Convert.ToDecimal(convertible, CultureInfo.InvariantCulture)),
            _ => null
        };
    }

    private static double? ConvertToDouble(object? value) {
        return value switch {
            null => null,
            double doubleValue => doubleValue,
            decimal decimalValue => (double)decimalValue,
            long longValue => longValue,
            int intValue => intValue,
            string stringValue when double.TryParse(
                stringValue,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            IConvertible convertible => TryConvert(
                () => Convert.ToDouble(convertible, CultureInfo.InvariantCulture)),
            _ => null
        };
    }

    private static bool? ConvertToBoolean(object? value) {
        return value switch {
            null => null,
            bool boolValue => boolValue,
            int intValue when intValue is 0 or 1 => intValue == 1,
            long longValue when longValue is 0 or 1 => longValue == 1,
            string stringValue when bool.TryParse(stringValue.Trim(), out var parsed) => parsed,
            string stringValue when stringValue.Trim() == "0" => false,
            string stringValue when stringValue.Trim() == "1" => true,
            IConvertible convertible => TryConvert(
                () => Convert.ToBoolean(convertible, CultureInfo.InvariantCulture)),
            _ => null
        };
    }

    private static T? TryConvert<T>(Func<T> converter)
        where T : struct {
        try {
            return converter();
        }
        catch {
            return null;
        }
    }

    private static InvalidOperationException CreateMissingOrInvalidValueException(
        string attributeName,
        string targetType) {
        return new InvalidOperationException(
            $"Attribute '{attributeName}' is missing, null, or cannot be converted to {targetType}.");
    }
}