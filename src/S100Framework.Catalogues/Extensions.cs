using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;



namespace System.Xml.Linq
{
    public static class Extension
    {
        public static string? GetCode(this XElement element) {
            var navigator = element.CreateNavigator();
            navigator.MoveToFollowing(XPathNodeType.Element);

            var scopes = navigator.GetNamespacesInScope(XmlNamespaceScope.All);

            return element.Element(XName.Get("code", scopes["S100FC"]))?.Value;
        }

        public static int? GetSourceIdentifier(this XElement element) {
            var navigator = element.CreateNavigator();
            navigator.MoveToFollowing(XPathNodeType.Element);

            var scopes = navigator.GetNamespacesInScope(XmlNamespaceScope.All);

            var source = element.Element(XName.Get("definitionReference", scopes["S100FC"]))?.Element(XName.Get("sourceIdentifier", scopes["S100FC"]))?.Value;

            if (string.IsNullOrEmpty(source)) return default;

            if (int.TryParse(source, out var code)) return code;

            return default;
        }

        public static string[] GetFeatureTypes(this XDocument ps, S100FC.Primitives primitive) {
            var navigator = ps.CreateNavigator();
            navigator.MoveToFollowing(XPathNodeType.Element);

            var scopes = navigator.GetNamespacesInScope(XmlNamespaceScope.All);

            ps.Descendants(XName.Get("S100_FC_FeatureType", scopes["S100FC"])).Select(e => e.Element(XName.Get("code", scopes["S100FC"]))!.Value).ToArray();

            //var xx = members.Elements(ft => ft.Element("permittedPrimitives")!.Value.Equals($"{primitive}"));

            //xx.Select(e => e.Parent!.Element(XName.Get("code", "")));
            return [];
        }

        //private static Regex _substitute = new Regex(@"^S(?<number>\d+)$", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);

        //public static IEnumerable<XElement> Features(this XDocument doc) {
        //    var members = doc.XPathSelectElement("//*[local-name()='members']");
        //    if (members is null)
        //        yield break;
        //    var prefix = members.GetPrefixOfNamespace(members.Name.NamespaceName);
        //    foreach (var member in members.Elements()) {
        //        yield return member;
        //    }
        //    yield break;
        //}

        //public static string Identifier(this XElement element) {
        //    return element.Attribute(XName.Get("id", element.GetNamespaceOfPrefix("gml")!.NamespaceName))!.Value;
        //}

        //public static AttributeModel.FeatureType? FeatureType(this XElement element) {
        //    var prefix = element.GetPrefixOfNamespace(element.Name.Namespace)!;

        //    var catalogue = FeatureCatalogue.Catalogues.Single(e => e.ProductID.Equals(_substitute.Replace(prefix, @"S-${number}")));

        //    var type = catalogue.Assembly!.GetType($"S100Framework.AttributeModel.{prefix}.FeatureTypes.{element.Name.LocalName}")!;
        //    var serializer = new XmlSerializer(type);
        //    return serializer.Deserialize(element.CreateReader()) as AttributeModel.FeatureType;
        //}
    }
}
