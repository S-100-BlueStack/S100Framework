using JsonFlatten;
//using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using S100FC;
using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace S100FC
{
    public class JsonUnflattener
    {
        public static JsonNode Unflatten(string jsonString) {
            // 1. Parse the flat string into a dictionary
            var flatDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);
            var root = new JsonObject();

            if (flatDict is null) return root;

            foreach (var kvp in flatDict) {
                // Split by dot, but ignore dots inside brackets if necessary 
                // (Simple split works for your example)
                string[] parts = kvp.Key.Split('.');
                ProcessPath(root, parts, kvp.Value);
            }

            return root;
        }

        public static IEnumerable<(string Path, object Value)> GetAllProperties(JsonNode node, string path = "") {
            if (node is JsonObject obj) {
                foreach (var prop in obj) {
                    string subPath = string.IsNullOrEmpty(path) ? prop.Key : $"{path}.{prop.Key}";
                    if (prop.Value is null) continue;
                    foreach (var child in GetAllProperties(prop.Value, subPath))
                        yield return child;
                }
            }
            else if (node is JsonArray array) {
                for (int i = 0; i < array.Count; i++) {
                    foreach (var child in GetAllProperties(array[i]!, $"{path}[{i}]"))
                        yield return child;
                }
            }
            else {
                // Try to get the underlying value (bool, double, string, etc.)
                object val = node?.AsValue().ToString(); // Simplified for example
                yield return (path, val);
            }
        }


        private static void ProcessPath(JsonObject currentParent, string[] parts, JsonElement value) {
            JsonNode currentNode = currentParent;

            for (int i = 0; i < parts.Length; i++) {
                string part = parts[i];
                bool isLast = (i == parts.Length - 1);

                // Check if the part indicates an array, e.g., "zoneOfConfidence[0]"
                var arrayMatch = Regex.Match(part, @"^(.+)\[(\d+)\]$");

                if (arrayMatch.Success) {
                    string arrayName = arrayMatch.Groups[1].Value;
                    int index = int.Parse(arrayMatch.Groups[2].Value);

                    // Ensure the array exists
                    if (!currentParent.ContainsKey(arrayName) || currentParent[arrayName] == null) {
                        currentParent[arrayName] = new JsonArray();
                    }

                    JsonArray array = currentParent[arrayName].AsArray();

                    // Expand array with nulls if index is higher than current count
                    while (array.Count <= index) { array.Add(null); }

                    if (isLast) {
                        array[index] = JsonValue.Create(value);
                    }
                    else {
                        // If not last, we need an object at this index to continue
                        if (array[index] == null) { array[index] = new JsonObject(); }
                        currentParent = array[index].AsObject();
                    }
                }
                else {
                    // It's a regular property
                    if (isLast) {
                        currentParent[part] = JsonValue.Create(value);
                    }
                    else {
                        if (!currentParent.ContainsKey(part) || currentParent[part] == null) {
                            currentParent[part] = new JsonObject();
                        }
                        currentParent = currentParent[part].AsObject();
                    }
                }
            }
        }
    }

    public class JsonFlattener {
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

        private readonly static JsonSerializerOptions jsonSerializerOptions = new() {
            WriteIndented = true,
            Converters = { new DecimalTrimConverter() },
        };

        public static string Flatten(this FeatureType feature) => FlattenAttributes(feature.attributeBindings, feature.attributeBindingsCatalogue);
        public static string Flatten(this InformationType information) => FlattenAttributes(information.attributeBindings, information.attributeBindingsCatalogue);
        
        /// <summary> 
        /// Creates and populates an instance of the specified type, FeatureType or InformationType, using the provided JSON attribute data and type code. 
        /// </summary> 
        /// <typeparam name="T"> 
        /// The target class type to instantiate. Supported types include <c>FeatureType</c> and <c>InformationType</c>. 
        /// </typeparam> 
        /// <param name="attributes"> 
        /// A JSON string containing the flattened attributes used to populate the object. 
        /// </param> 
        /// <param name="code"> 
        /// The code identifying the concrete type to instantiate within the feature catalogue. 
        /// </param> 
        /// <returns> A fully populated instance of <typeparamref name="T"/> created from the JSON data. 
        /// </returns>
        public static T Unflatten<T>(string? attributes, Type type) where T : class {
            if (string.IsNullOrWhiteSpace(attributes)) {
                return (T)Activator.CreateInstance(type)!;
            }
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonValue>>(attributes) ?? [];
            var root = (JsonObject)Unflatten(dict);

            //var featureCatalogue = S100FC.Catalogues.FeatureCatalogue.Catalogues.Single(e => e.ProductID.Equals("S-101"));
            //var type = featureCatalogue.Assembly!.GetType($"{S100FC.Catalogues.FeatureCatalogue.Namespace("S101", $"{typeof(T).Name}s")}.{code}", true)!;

            var instance = Activator.CreateInstance(type) as T;

            PopulateObject(instance!, root);
            return instance!;
        }

        private static void PopulateObject(object target, JsonObject json) {
            var type = target.GetType();

            foreach (var (name, node) in json) {
                var prop = type.GetProperty(name);
                if (prop == null || !prop.CanWrite)
                    continue;

                var propType = prop.PropertyType;

                // null
                if (node is null) {
                    prop.SetValue(target, null);
                    continue;
                }

                // primitive
                if (node is JsonValue value) {
                    var converted = value.Deserialize(propType);
                    prop.SetValue(target, converted);
                    continue;
                }

                // array
                if (node is JsonArray array) {
                    var elementType = propType.GetElementType()
                        ?? propType.GetGenericArguments().First();

                    var list = (IList)Activator.CreateInstance(
                        typeof(List<>).MakeGenericType(elementType)
                    )!;

                    foreach (var item in array) {
                        if (item is JsonValue v) {
                            list.Add(v.Deserialize(elementType));
                        }
                        else if (item is JsonObject o) {
                            var elem = Activator.CreateInstance(elementType)!;
                            PopulateObject(elem, o);
                            list.Add(elem);
                        }
                    }

                    prop.SetValue(
                        target,
                        propType.IsArray ? ToArray(list, elementType) : list
                    );
                    continue;
                }

                // complex object
                if (node is JsonObject obj) {
                    var child = Activator.CreateInstance(propType)!;
                    PopulateObject(child, obj);
                    prop.SetValue(target, child);
                }
            }
        }
        private static Array ToArray(IList list, Type elementType) {
            var arr = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(arr, 0);
            return arr;
        }
        private static JsonNode Unflatten(Dictionary<string, JsonValue> source) {
            var regex = new System.Text.RegularExpressions.Regex(@"(?!\.)([^. ^\[\]]+)|(?!\[)(\d+)(?=\])");
            JsonNode node = JsonNode.Parse("{}");

            foreach (var keyValue in source) {
                var pathSegments = regex.Matches(keyValue.Key).Select(m => m.Value).ToArray();

                for (int i = 0; i < pathSegments.Length; i++) {
                    var currentSegmentType = GetSegmentKind(pathSegments[i]);

                    if (currentSegmentType == JsonValueKind.Object) {
                        if (node[pathSegments[i]] == null) {
                            if (pathSegments[i] == pathSegments[pathSegments.Length - 1]) {
                                node[pathSegments[i]] = keyValue.Value;
                                node = node.Root;
                            }
                            else {
                                var nextSegmentType = GetSegmentKind(pathSegments[i + 1]);

                                if (nextSegmentType == JsonValueKind.Object) {
                                    node[pathSegments[i]] = JsonNode.Parse("{}");
                                }
                                else {
                                    node[pathSegments[i]] = JsonNode.Parse("[]");
                                }
                                node = node[pathSegments[i]];
                            }
                        }
                        else {
                            node = node[pathSegments[i]];
                        }
                    }
                    else {
                        if (!int.TryParse(pathSegments[i], out int index)) {
                            throw new Exception("Cannot parse index");
                        }

                        while (node.AsArray().Count - 1 < index) {
                            node.AsArray().Add(null);
                        }

                        if (i == pathSegments.Length - 1) {
                            node[index] = keyValue.Value;
                            node = node.Root;
                        }
                        else {
                            if (node[index] == null) {
                                var nextSegmentType = GetSegmentKind(pathSegments[i + 1]);

                                if (nextSegmentType == JsonValueKind.Object) {
                                    node[index] = JsonNode.Parse("{}");
                                }
                                else {
                                    node[index] = JsonNode.Parse("[]");
                                }
                            }

                            node = node[index];
                        }
                    }
                }
            }

            return node;
        }
        private static JsonValueKind GetSegmentKind(string pathSegment) => int.TryParse(pathSegment, out _) ? JsonValueKind.Array : JsonValueKind.Object;

        internal static string FlattenAttributes(attributeBinding[] attributes, attributeBindingDefinition[] catalogue) {
            var root = new JObject();

            foreach (var attr in attributes) {
                if (attr.S100FC_code.StartsWith("wave")) System.Diagnostics.Debugger.Break();
                var isCollection = catalogue.Single(e => e.attribute == attr.S100FC_code).IsCollection;

                AddAttribute(root, attr, catalogue, isCollection);
            }

            var flattened = root.Flatten();
            return JsonSerializer.Serialize(flattened, jsonSerializerOptions);
        }
        private static void AddAttribute(JObject parent, attributeBinding attribute, attributeBindingDefinition[] catalogue, bool isCollection = false) {
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
                    AddValue(parent, attribute.S100FC_code, s.value, isCollection);
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
                    if (isCollection) {
                        var array = parent[attribute.S100FC_code] as JArray ?? [];
                        var obj = new JObject();

                        foreach (var child in c.attributeBindings) {
                            var childDef = c.attributeBindingsCatalogue.Single(d => d.attribute == child.S100FC_code);
                            AddAttribute(obj, child, c.attributeBindingsCatalogue, childDef.IsCollection);
                        }

                        array.Add(obj);
                        parent[attribute.S100FC_code] = array;
                    }
                    else {
                        var obj = new JObject();
                        foreach (var child in c.attributeBindings) {
                            var childDef = c.attributeBindingsCatalogue.Single(d => d.attribute == child.S100FC_code);
                            AddAttribute(obj, child, c.attributeBindingsCatalogue, childDef.IsCollection);
                        }
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