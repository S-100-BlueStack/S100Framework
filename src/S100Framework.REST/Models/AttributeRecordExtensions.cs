using System.Globalization;

namespace S100Framework.REST.Models;

/// <summary>
/// Provides typed helpers for reading attribute values from <see cref="IAttributeRecord"/>.
/// </summary>
public static class AttributeRecordExtensions
{
    /// <summary>
    /// Determines whether the record contains the specified attribute key.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the attribute exists; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="record"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="attributeName"/> is empty or whitespace.
    /// </exception>
    public static bool HasAttribute(this IAttributeRecord record, string attributeName) {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        return record.Attributes.ContainsKey(attributeName);
    }

    /// <summary>
    /// Gets the raw attribute value for the specified key.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The raw attribute value, or <see langword="null"/> when the attribute is missing.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="record"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="attributeName"/> is empty or whitespace.
    /// </exception>
    public static object? GetRawValue(this IAttributeRecord record, string attributeName) {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        return record.Attributes.TryGetValue(attributeName, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="string"/>.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted string value, or <see langword="null"/> when the attribute is missing
    /// or cannot be converted.
    /// </returns>
    public static string? GetString(this IAttributeRecord record, string attributeName) {
        return ConvertToString(record.GetRawValue(attributeName));
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="string"/>, throwing when the value
    /// is missing or invalid.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted string value.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attribute is missing, null, or cannot be converted to a string.
    /// </exception>
    public static string GetRequiredString(this IAttributeRecord record, string attributeName) {
        var value = record.GetString(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "string");
        }

        return value;
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="int"/>.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted integer value, or <see langword="null"/> when the attribute is missing
    /// or cannot be converted.
    /// </returns>
    public static int? GetInt32(this IAttributeRecord record, string attributeName) {
        return ConvertToInt32(record.GetRawValue(attributeName));
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="int"/>, throwing when the value
    /// is missing or invalid.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted integer value.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attribute is missing, null, or cannot be converted to <see cref="int"/>.
    /// </exception>
    public static int GetRequiredInt32(this IAttributeRecord record, string attributeName) {
        var value = record.GetInt32(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Int32");
        }

        return value.Value;
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="long"/>.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted 64-bit integer value, or <see langword="null"/> when the attribute
    /// is missing or cannot be converted.
    /// </returns>
    public static long? GetInt64(this IAttributeRecord record, string attributeName) {
        return ConvertToInt64(record.GetRawValue(attributeName));
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="long"/>, throwing when the value
    /// is missing or invalid.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted 64-bit integer value.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attribute is missing, null, or cannot be converted to <see cref="long"/>.
    /// </exception>
    public static long GetRequiredInt64(this IAttributeRecord record, string attributeName) {
        var value = record.GetInt64(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Int64");
        }

        return value.Value;
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="decimal"/>.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted decimal value, or <see langword="null"/> when the attribute is missing
    /// or cannot be converted.
    /// </returns>
    public static decimal? GetDecimal(this IAttributeRecord record, string attributeName) {
        return ConvertToDecimal(record.GetRawValue(attributeName));
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="decimal"/>, throwing when the value
    /// is missing or invalid.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted decimal value.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attribute is missing, null, or cannot be converted to <see cref="decimal"/>.
    /// </exception>
    public static decimal GetRequiredDecimal(this IAttributeRecord record, string attributeName) {
        var value = record.GetDecimal(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Decimal");
        }

        return value.Value;
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="double"/>.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted double value, or <see langword="null"/> when the attribute is missing
    /// or cannot be converted.
    /// </returns>
    public static double? GetDouble(this IAttributeRecord record, string attributeName) {
        return ConvertToDouble(record.GetRawValue(attributeName));
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="double"/>, throwing when the value
    /// is missing or invalid.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted double value.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attribute is missing, null, or cannot be converted to <see cref="double"/>.
    /// </exception>
    public static double GetRequiredDouble(this IAttributeRecord record, string attributeName) {
        var value = record.GetDouble(attributeName);

        if (value is null) {
            throw CreateMissingOrInvalidValueException(attributeName, "Double");
        }

        return value.Value;
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="bool"/>.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted boolean value, or <see langword="null"/> when the attribute is missing
    /// or cannot be converted.
    /// </returns>
    public static bool? GetBoolean(this IAttributeRecord record, string attributeName) {
        return ConvertToBoolean(record.GetRawValue(attributeName));
    }

    /// <summary>
    /// Gets the attribute value converted to <see cref="bool"/>, throwing when the value
    /// is missing or invalid.
    /// </summary>
    /// <param name="record">
    /// The attribute record.
    /// </param>
    /// <param name="attributeName">
    /// The attribute name to look up.
    /// </param>
    /// <returns>
    /// The converted boolean value.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attribute is missing, null, or cannot be converted to <see cref="bool"/>.
    /// </exception>
    public static bool GetRequiredBoolean(this IAttributeRecord record, string attributeName) {
        var value = record.GetBoolean(attributeName);

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
            IConvertible convertible => TryConvert(() => Convert.ToInt32(convertible, CultureInfo.InvariantCulture)),
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
            IConvertible convertible => TryConvert(() => Convert.ToInt64(convertible, CultureInfo.InvariantCulture)),
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
            IConvertible convertible => TryConvert(() => Convert.ToDecimal(convertible, CultureInfo.InvariantCulture)),
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
            IConvertible convertible => TryConvert(() => Convert.ToDouble(convertible, CultureInfo.InvariantCulture)),
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
            IConvertible convertible => TryConvert(() => Convert.ToBoolean(convertible, CultureInfo.InvariantCulture)),
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