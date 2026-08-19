using System;
using System.Collections.Generic;
using System.Text;

namespace S100FC.S101
{
    using ArcGIS.Core.Data;
    using S100FC.S101.FeatureTypes;
    using S100FC.S101.SimpleAttributes;
    using System.Data;

    public static class S101Extensions
    {
        public static IEnumerable<(DataCoverage DataCoverage, SpatialQueryFilter SpatialQueryFilter)> QueryDataCoverages(this Geodatabase geodatabase, string datasetName) {
            if (string.IsNullOrEmpty(datasetName)) yield break;

            var syntax = geodatabase.GetSQLSyntax();

            var definitionTables = geodatabase.GetDefinitions<TableDefinition>();
            var definitionFeatures = geodatabase.GetDefinitions<FeatureClassDefinition>();

            using var surface = geodatabase.OpenDataset<FeatureClass>(definitionFeatures.Single(e => syntax.ParseTableName(e.GetName()).Item3.Equals("surface")).GetName());

            using var cursor = surface.Search(new QueryFilter {
                WhereClause = $"upper(ps) = 'S-128' and attributeBindings LIKE '%\"datasetName\":%\"{datasetName.ToUpperInvariant()}\"%'",
            }, true);

            if (!cursor.MoveNext()) yield break;
               
            {
                var current = (ArcGIS.Core.Data.Feature)cursor.Current;

                var electricProduct = (S100FC.S128.FeatureTypes.ElectronicProduct)S100FC.AttributeFlattenExtensions.Unflatten<S100FC.FeatureType>(Convert.ToString(current["attributebindings"])!, typeof(S100FC.S128.FeatureTypes.ElectronicProduct));

                var shape = (ArcGIS.Core.Geometry.Polygon)current.GetShape().Clone();

                var whereClause = "upper(ps) = 'S-101'";

                SpatialQueryFilter[] spatialQueryFilters = [];

                using var datacoverageSearch = surface.Search(new SpatialQueryFilter {
                    WhereClause = $"upper(ps) = 'S-101' AND code = 'DataCoverage' AND attributeBindings LIKE '%\"minimumDisplayScale\":%{electricProduct.minimumDisplayScale}%'",
                    FilterGeometry = shape,
                    SpatialRelationship = SpatialRelationship.Contains,
                }, true);

                while (datacoverageSearch.MoveNext()) {
                    var f = (ArcGIS.Core.Data.Feature)datacoverageSearch.Current;

                    var dataCoverage = (S100FC.S101.FeatureTypes.DataCoverage)S100FC.AttributeFlattenExtensions.Unflatten<S100FC.FeatureType>(Convert.ToString(f["attributebindings"])!, typeof(S100FC.S101.FeatureTypes.DataCoverage));

                    var spatialQueryFilter = new SpatialQueryFilter {
                        WhereClause = whereClause + $" AND nominalscale = {dataCoverage.optimumDisplayScale}",
                        FilterGeometry = f.GetShape().Clone(),
                        SpatialRelationship = SpatialRelationship.Relation,
                        SpatialRelationshipDescription = "UNKNOWN",
                        SubFields = "OBJECTID,UID,GLOBALID,CODE,SHAPE",
                    };

                    yield return (dataCoverage, spatialQueryFilter);
                }
            }
            yield break;
        }
    }
}
