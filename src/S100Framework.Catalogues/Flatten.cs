using JsonFlatten;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace S100FC
{
    public class JsonUnflattener
    {
        public static JsonNode Unflatten(string jsonString) {
            var flatDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);
            var root = new JsonObject();

            if (flatDict is null) return root;

            foreach (var kvp in flatDict) {
                string[] parts = kvp.Key.Split('.');
                ProcessPath(root, parts, kvp.Value);
            }

            return root;
        }

        public static IEnumerable<(string Path, object? Value)> GetAllProperties(JsonNode? node, string path = "") {
            if (node is null) {
                // JsonNode represents JSON null with a null reference.
                yield return (path, null);
                yield break;
            }

            if (node is JsonObject obj) {
                foreach (var prop in obj) {
                    string subPath = string.IsNullOrEmpty(path) ? prop.Key : $"{path}.{prop.Key}";
                    foreach (var child in GetAllProperties(prop.Value, subPath))
                        yield return child;
                }
            }
            else if (node is JsonArray array) {
                for (int i = 0; i < array.Count; i++) {
                    foreach (var child in GetAllProperties(array[i], $"{path}[{i}]"))
                        yield return child;
                }
            }
            else {
                object val = node.AsValue().ToString();
                yield return (path, val);
            }
        }


        private static void ProcessPath(JsonObject currentParent, string[] parts, JsonElement value) {
            for (int i = 0; i < parts.Length; i++) {
                string part = parts[i];
                bool isLast = (i == parts.Length - 1);

                // Check if the part indicates an array, e.g., "zoneOfConfidence[0]"
                var arrayMatch = Regex.Match(part, @"^(.+)\[(\d+)\]$");

                if (arrayMatch.Success) {
                    string arrayName = arrayMatch.Groups[1].Value;
                    int index = int.Parse(arrayMatch.Groups[2].Value, CultureInfo.InvariantCulture);

                    // Ensure the array exists
                    if (!currentParent.ContainsKey(arrayName) || currentParent[arrayName] == null) {
                        currentParent[arrayName] = new JsonArray();
                    }

                    JsonArray array = currentParent[arrayName]!.AsArray();

                    // Expand array with nulls if index is higher than current count
                    while (array.Count <= index) { array.Add(null); }

                    if (isLast) {
                        array[index] = JsonNode.Parse(value.GetRawText());
                    }
                    else {
                        // If not last, we need an object at this index to continue
                        if (array[index] == null) { array[index] = new JsonObject(); }
                        currentParent = array[index]!.AsObject();
                    }
                }
                else {
                    // It's a regular property
                    if (isLast) {
                        currentParent[part] = JsonNode.Parse(value.GetRawText());
                    }
                    else {
                        if (!currentParent.ContainsKey(part) || currentParent[part] == null) {
                            currentParent[part] = new JsonObject();
                        }
                        currentParent = currentParent[part]!.AsObject();
                    }
                }
            }
        }
    }

    public class JsonFlattener
    {
        //public static string Flatten(featureBinding binding) => AttributeFlattenExtensions.Flatten(binding);

        //public static string Flatten(informationBinding binding) => AttributeFlattenExtensions.Flatten(binding);

        public static string Flatten(attributeBinding[] attributeBindings, attributeBindingDefinition[] attributeBindingDefinitions) {
            return AttributeFlattenExtensions.FlattenAttributes(attributeBindings, attributeBindingDefinitions);
        }
    }
}

namespace S100FC
{
    public static class AttributeFlattenExtensions
    {
        public class DecimalTrimConverter : System.Text.Json.Serialization.JsonConverter<decimal>
        {
            public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => reader.GetDecimal();

            public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
                => writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
        }

        private static readonly JsonSerializerOptions jsonSerializerOptions = new() {
            WriteIndented = true,
            Converters = { new DecimalTrimConverter(), new JsonStringEnumConverter() },
        };

        public static string Flatten(this FeatureType feature) => FlattenAttributes(feature.attributeBindings, feature.attributeBindingsCatalogue);
        public static string Flatten(this InformationType information) => FlattenAttributes(information.attributeBindings, information.attributeBindingsCatalogue);

        //public static string Flatten(this featureBinding binding) {
        //    ArgumentNullException.ThrowIfNull(binding);

        //    return SerializeFlattened(CreateFeatureBinding(binding), includeNullAndEmptyValues: true);
        //}

        public static string Flatten(this featureBinding[]? bindings) {
            var root = new JObject();

            if (bindings is { Length: > 0 }) {
                var items = new JArray();
                foreach (var binding in bindings) {
                    items.Add(binding is null ? JValue.CreateNull() : CreateFeatureBinding(binding));
                }
                root["featureBinding"] = items;
            }
            return SerializeFlattened(root, includeNullAndEmptyValues: true);
        }

        //public static string Flatten(this informationBinding binding) {
        //    ArgumentNullException.ThrowIfNull(binding);

        //    return SerializeFlattened(CreateInformationBinding(binding), includeNullAndEmptyValues: true);
        //}

        public static string Flatten(this informationBinding[]? bindings) {
            var root = new JObject();

            if (bindings is { Length: > 0 }) {
                var items = new JArray();
                foreach (var binding in bindings) {
                    items.Add(binding is null ? JValue.CreateNull() : CreateInformationBinding(binding));
                }
                root["informationBinding"] = items;
            }
            return SerializeFlattened(root, includeNullAndEmptyValues: true);
        }

        private static JObject CreateFeatureBinding(featureBinding binding) {
            // Share the association contract so arrays do not fall back to Newtonsoft's
            // reflection serializer, which ignores System.Text.Json annotations.
            return new JObject {
                ["association"] = CreateAssociation(binding.association),
                ["roleType"] = binding.roleType,
                ["role"] = binding.role,
                ["featureType"] = binding.featureType,
                ["featureId"] = binding.featureId,
                // This projection bypasses property converters, so emit the enum name explicitly.
                ["Primitive"] = binding.Primitive?.ToString(),
            };
        }

        private static JObject CreateInformationBinding(informationBinding binding) {
            // Share the association contract so arrays do not fall back to Newtonsoft's
            // reflection serializer, which ignores System.Text.Json annotations.
            return new JObject {
                ["association"] = CreateAssociation(binding.association),
                ["roleType"] = binding.roleType,
                ["role"] = binding.role,
                ["informationType"] = binding.informationType,
                ["informationId"] = binding.informationId,
            };
        }

        private static JToken CreateAssociation(association? association) {
            if (association is null) return JValue.CreateNull();

            return new JObject {
                ["code"] = association.S100FC_code,
                ["attributes"] = CreateAttributes(
                    association.attributeBindings,
                    association.attributeBindingsCatalogue),
            };
        }

        /// <summary>Restores feature attributes using a concrete FeatureType class.</summary>
        public static T UnflattenFeatureType<T>(string? attributes) where T : FeatureType
            => (T)Unflatten<FeatureType>(attributes, typeof(T), associationType: null);

        public static FeatureType UnflattenFeatureType(string? attributes, Type featureType)
            => Unflatten<FeatureType>(attributes, featureType, associationType: null);

        /// <summary>Restores information attributes using a concrete InformationType class.</summary>
        public static T UnflattenInformationType<T>(string? attributes) where T : InformationType
            => (T)Unflatten<InformationType>(attributes, typeof(T), associationType: null);

        public static InformationType UnflattenInformationType(string? attributes, Type informationType)
            => Unflatten<InformationType>(attributes, informationType, associationType: null);

        /// <summary>
        /// Restores information bindings. Supply associationType when association attributes
        /// require a concrete association class. One type applies to all associations in the array.
        /// </summary>
        public static informationBinding[] UnflattenInformationBindings(string? attributes,
            Type? associationType = null)
            => Unflatten<informationBinding[]>(attributes, associationType);

        /// <summary>
        /// Restores feature bindings. Supply associationType when association attributes
        /// require a concrete association class. One type applies to all associations in the array.
        /// </summary>
        public static featureBinding[] UnflattenFeatureBindings(string? attributes,
            Type? associationType = null)
            => Unflatten<featureBinding[]>(attributes, associationType);

        public static T UnflattenRaw<T>(string? attributes,
            Type? associationType = null) where T : class
            => Unflatten<T>(attributes, typeof(T), associationType);

        /// <summary>
        /// Restores a feature, information type, binding, or wrapped array.
        /// Supply associationType for base bindings that contain association attributes.
        /// Generic bindings retain their constructor-created concrete association.
        /// </summary>
        /// <param name="attributes">Flattened JSON produced by the matching flatten method.</param>
        /// <param name="associationType">
        /// Optional concrete association class with a public parameterless constructor.
        /// The same type applies to all base associations in this unflatten operation.
        /// </param>
        private static T Unflatten<T>(string? attributes,
            Type? associationType = null) where T : class
            => Unflatten<T>(attributes, typeof(T), associationType);

        private static T Unflatten<T>(string? attributes, Type type,
            Type? associationType) where T : class {
            ArgumentNullException.ThrowIfNull(type);
            if (!typeof(T).IsAssignableFrom(type)) {
                throw new ArgumentException($"{type} must be assignable to {typeof(T)}.", nameof(type));
            }

            if (type.IsAbstract || type.IsInterface) {
                throw new NotSupportedException($"{type} must be a concrete type.");
            }

            if (associationType is not null
                && (associationType == typeof(association)
                    || !typeof(association).IsAssignableFrom(associationType)
                    || associationType.IsAbstract || associationType.ContainsGenericParameters
                    || associationType.GetConstructor(Type.EmptyTypes) is null)) {
                throw new ArgumentException(
                    $"{associationType} must derive from association and be a concrete type with a public parameterless constructor.",
                    nameof(associationType));
            }

            if (type.IsArray) {
                if (!type.IsSZArray) {
                    throw new NotSupportedException("Only single-dimensional, zero-based arrays are supported.");
                }

                var elementType = type.GetElementType()!;
                if (string.IsNullOrWhiteSpace(attributes)) {
                    return (T)(object)Array.CreateInstance(elementType, 0);
                }

                var root = (JsonObject)JsonUnflattener.Unflatten(attributes);
                // Binding array flatteners wrap their items in a named array.
                // An empty input collection is serialized as an empty object.
                if (root.Count == 0) {
                    return (T)(object)Array.CreateInstance(elementType, 0);
                }

                if (root.Count != 1 || root.First().Value is not JsonArray items) {
                    throw new JsonException("An array target requires a single named array in the flattened JSON.");
                }

                return (T)(object)ToArray(PopulateList(items, elementType, associationType), elementType);
            }

            var json = string.IsNullOrWhiteSpace(attributes)
                ? new JsonObject()
                : (JsonObject)JsonUnflattener.Unflatten(attributes);
            var instance = (T)CreateObject(type, associationType);
            PopulateObject(instance, json, associationType);

            return instance;
        }

        private static void PopulateObject(object target, JsonObject json,
            Type? associationType) {
            var type = target.GetType();

            // Validate before attribute setters run, regardless of JSON property order.
            if (target is association concreteAssociation && type != typeof(association)
                && json["code"] is JsonValue codeNode) {
                var code = codeNode.GetValue<string>();
                if (!string.Equals(code, concreteAssociation.S100FC_code, StringComparison.Ordinal)) {
                    throw new JsonException($"Association '{code}' does not match {type}.");
                }
            }

            foreach (var (name, node) in json) {
                if (target is association association) {
                    if (name == "attributes" && node is JsonObject attributes) {
                        if (type == typeof(association) && attributes.Count > 0) {
                            throw new JsonException(
                                "Association attributes require a concrete association type. " +
                                "Pass associationType to Unflatten, or use a generic binding with a concrete association.");
                        }

                        // Generated association properties update the underlying attribute bindings.
                        PopulateObject(association, attributes, associationType);
                        continue;
                    }
                }

                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                    ?? type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .SingleOrDefault(p =>
                            p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == name);

                if (prop == null || !prop.CanWrite || prop.GetIndexParameters().Length != 0)
                    continue;

                if (node is null) {
                    prop.SetValue(target, null);
                }
                else if (node is JsonValue value) {
                    prop.SetValue(target, value.Deserialize(prop.PropertyType, jsonSerializerOptions));
                }
                else if (node is JsonArray array) {
                    var elementType = prop.PropertyType.GetElementType()
                        ?? prop.PropertyType.GetGenericArguments().First();
                    var list = PopulateList(array, elementType, associationType);

                    prop.SetValue(target, prop.PropertyType.IsArray ? ToArray(list, elementType) : list);
                }
                else if (node is JsonObject obj) {
                    var existing = prop.CanRead ? prop.GetValue(target) : null;
                    var child = CreateObject(prop.PropertyType, associationType, existing);
                    PopulateObject(child, obj, associationType);
                    prop.SetValue(target, child);
                }
            }
        }

        private static object CreateObject(Type type, Type? associationType, object? existing = null) {
            if (type == typeof(association) && associationType is not null) {
                if (existing is not null && existing.GetType() != typeof(association)) {
                    // A generic binding already fixes the association type in its constructor.
                    if (!associationType.IsInstanceOfType(existing)) {
                        throw new JsonException(
                            $"The existing association {existing.GetType()} does not match the supplied association type {associationType}.");
                    }
                    return existing;
                }

                return Activator.CreateInstance(associationType)
                    ?? throw new InvalidOperationException($"Cannot create {associationType}.");
            }

            return existing ?? Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Cannot create {type}.");
        }

        private static IList PopulateList(JsonArray array, Type elementType,
            Type? associationType) {
            var list = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(elementType))!;

            foreach (var item in array) {
                if (item is null) {
                    if (elementType.IsValueType && Nullable.GetUnderlyingType(elementType) is null) {
                        throw new JsonException($"Null is not valid for array element type {elementType}.");
                    }
                    list.Add(null);
                }
                else if (item is JsonValue value) {
                    list.Add(value.Deserialize(elementType, jsonSerializerOptions));
                }
                else if (item is JsonObject obj) {
                    // Preserve constructor-created associations and generated attribute setters.
                    var element = CreateObject(elementType, associationType);
                    PopulateObject(element, obj, associationType);
                    list.Add(element);
                }
                else if (item is JsonArray nested && elementType.IsSZArray) {
                    var nestedElementType = elementType.GetElementType()!;
                    list.Add(ToArray(PopulateList(nested, nestedElementType, associationType), nestedElementType));
                }
                else {
                    throw new JsonException($"Unsupported array item for element type {elementType}.");
                }
            }

            return list;
        }

        private static Array ToArray(IList list, Type elementType) {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        private static string SerializeFlattened(JObject root, bool includeNullAndEmptyValues = true) {
            var flattened = root.Flatten(includeNullAndEmptyValues: includeNullAndEmptyValues);
            return JsonSerializer.Serialize(flattened, jsonSerializerOptions);
        }

        internal static string FlattenAttributes(attributeBinding[] attributes, attributeBindingDefinition[] catalogue)
            => SerializeFlattened(CreateAttributes(attributes, catalogue), true);

        private static JObject CreateAttributes(attributeBinding[] attributes, attributeBindingDefinition[] catalogue) {
            var root = new JObject();

            foreach (var attribute in attributes) {
                var definition = catalogue.Single(d => d.attribute == attribute.S100FC_code);
                AddAttribute(root, attribute, definition.IsCollection);
            }

            return root;
        }

        private static void AddAttribute(JObject parent, attributeBinding attribute, bool isCollection = false) {
            switch (attribute) {
                case BooleanAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case IntegerAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case RealAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case TextAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case S100_TruncatedDateAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case DateAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case DateTimeAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case TimeAttribute s:
                    // Newtonsoft does not use the System.Text.Json converter on S100.Time.
                    AddValue(parent, attribute.S100FC_code, s.value?.ToString(), isCollection);
                    break;
                case UrnAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case UrlAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case UriAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case EnumerationAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case CodeListAttribute s:
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
                    break;
                case ComplexAttribute c:
                    var obj = CreateAttributes(c.attributeBindings, c.attributeBindingsCatalogue);
                    if (isCollection) {
                        var array = parent[attribute.S100FC_code] as JArray ?? [];
                        array.Add(obj);
                        parent[attribute.S100FC_code] = array;
                    }
                    else {
                        parent[attribute.S100FC_code] = obj;
                    }
                    break;
            }
        }
        private static void AddValue(JObject parent, string key, object? value, bool isCollection) {
            if (isCollection) {
                var array = parent[key] as JArray ?? [];
                var token = value != null ? JToken.FromObject(value) : JValue.CreateNull();

                array.Add(token);
                parent[key] = array;
            }
            else {
                var token = value != null ? JToken.FromObject(value) : JValue.CreateNull();

                parent[key] = token;
            }
        }
    }
}
