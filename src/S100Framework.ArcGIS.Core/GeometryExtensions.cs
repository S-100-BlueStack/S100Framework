using ArcGIS.Core.Data;
using ArcGIS.Core.Internal.Geometry;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using System.Globalization;

namespace ArcGIS.Core.Geometry
{
    public static class GeometryExtensions
    {
        public static Geometry BuildGeometry(string type, string[][] coordinates, int wkid = 4326) {
            var spatialReference = SpatialReferenceBuilder.CreateSpatialReference(wkid);

            switch (type) {
                case "pointproperty": {
                        var coords = coordinates[0];
                        var point = coords.Length switch {
                            2 => MapPointBuilderEx.CreateMapPoint(
                                    double.Parse(coords[1], CultureInfo.InvariantCulture),
                                    double.Parse(coords[0], CultureInfo.InvariantCulture),
                                    spatialReference),
                            3 => MapPointBuilderEx.CreateMapPoint(
                                    double.Parse(coords[1], CultureInfo.InvariantCulture),
                                    double.Parse(coords[0], CultureInfo.InvariantCulture),
                                    double.Parse(coords[2], CultureInfo.InvariantCulture),
                                    spatialReference),
                            _ => throw new InvalidOperationException(),
                        };
                        return point;
                    }
                case "curveproperty": {
                        var coords = coordinates[0];

                        return CreateLinearRing(coords, spatialReference);
                    }
                case "surfaceproperty": {
                        // Populate exterior ring
                        var exteriorCoords = coordinates[0];
                        var exterior = CreateLinearRing(exteriorCoords, spatialReference);

                        var polygonBuilder = new PolygonBuilderEx(exterior);

                        // Populate interior rings. Skip the first (exterior)
                        foreach (var interiorRing in coordinates.Skip(1)) {
                            var interior = CreateLinearRing(interiorRing, spatialReference);
                            polygonBuilder.AddPart(interior.Parts.First());
                        }

                        return polygonBuilder.ToGeometry();
                    }
                default:
                    throw new InvalidOperationException($"Invalid geometry type detected: {type}");
            }
        }


        static readonly SpatialReference spatialReference = SpatialReferenceBuilder.CreateSpatialReference(4326);

        static readonly GeometryFactory factory = new GeometryFactory(new PrecisionModel(10000000), srid: 4326); // Or PrecisionModels.Floating


        public static S100FC.Topology.IMatrix? BuildTopology(this Geodatabase geodatabase, QueryFilter? queryFilter = default, Action<ICollection<LineString>>? interceptor = default) {
            queryFilter = queryFilter switch {
                SpatialQueryFilter spatial => new SpatialQueryFilter {
                    FilterGeometry = spatial.FilterGeometry,
                    ObjectIDs = spatial.ObjectIDs,
                    Offset = spatial.Offset,
                    OutputSpatialReference = spatial.OutputSpatialReference,
                    PostfixClause = spatial.PostfixClause,
                    PrefixClause = spatial.PrefixClause,
                    RowCount = spatial.RowCount,
                    SearchOrder = spatial.SearchOrder,
                    SpatialRelationship = spatial.SpatialRelationship,
                    SpatialRelationshipDescription = spatial.SpatialRelationshipDescription,
                    SubFields = spatial.SubFields,
                    WhereClause = $"({spatial.WhereClause})",
                },
                QueryFilter filter => new QueryFilter {
                    ObjectIDs = filter.ObjectIDs,
                    Offset = filter.Offset,
                    OutputSpatialReference = filter.OutputSpatialReference,
                    PostfixClause = filter.PostfixClause,
                    PrefixClause = filter.PrefixClause,
                    RowCount = filter.RowCount,
                    SubFields = filter.SubFields,
                    WhereClause = filter.WhereClause,
                },
                _ => new QueryFilter {
                    WhereClause = "upper(ps) = 'S-101'",
                },
            };

            var whereClause = queryFilter.WhereClause;
            var prefix = queryFilter.PrefixClause;

            S100FC.Topology.Matrix.Factory = factory;

            var definitions = geodatabase.GetDefinitions<FeatureClassDefinition>();


            var matrix = S100FC.Topology.Matrix.CreateMatrix(interceptor);

            S100FC.Topology.ITopologyBuilder? builder = default;

            //  Skin of the Earth
            {
                var polygons = new List<S100FC.Topology.Polygon>();

                using (var surface = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => e.GetAliasName().Equals("surface")).GetName())) {
                    queryFilter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) IN ('DEPTHAREA','DREDGEDAREA','LANDAREA','UNSURVEYEDAREA'))";

                    using var cursor = surface.Search(queryFilter);

                    while (cursor.MoveNext()) {
                        var f = (Feature)cursor.Current;

                        var shape = (ArcGIS.Core.Geometry.Polygon)f.GetShape();

                        var name = Convert.ToString(f["UID"]);// f.Crc32(); UID
                        if (string.IsNullOrEmpty(name))
                            name = string.Empty;

                        var exteriorRing = shape.GetExteriorRing(0);
                        var coordinates = exteriorRing.Parts[0].Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                        var ex = (LineString)factory.CreateLineString([.. coordinates, coordinates[0]]);
                        ex = ex.RemoveRepeatedVertices();

                        if (shape.PartCount > 1) {
                            var interiorRings = new List<LineString>();

                            foreach (var interiorRing in shape.Parts.Skip(1)) {
                                coordinates = interiorRing.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                                var linestring = (LineString)factory.CreateLineString([.. coordinates, coordinates[0]]);
                                linestring = linestring.RemoveRepeatedVertices();
                                interiorRings.Add(linestring);
                            }

                            polygons.Add(new S100FC.Topology.Polygon(f.GetObjectID(), name, Convert.ToString(f["code"])!, ex, interiorRings.ToArray()));
                        }
                        else {
                            polygons.Add(new S100FC.Topology.Polygon(f.GetObjectID(), name, Convert.ToString(f["code"])!, ex, []));
                        }
                    }
                }

                var curves = new List<S100FC.Topology.Polyline>();

                using (var curve = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => e.GetAliasName().Equals("curve")).GetName())) {
                    queryFilter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) IN ('COASTLINE','DEPTHCONTOUR','SHORELINECONSTRUCTION'))";

                    using (var cursor = curve.Search(queryFilter)) {
                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            var shape = (ArcGIS.Core.Geometry.Polyline)f.GetShape();

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            var coordinates = shape.Points.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.X, segment.Y)).ToArray();

                            var linestring = (LineString)factory.CreateLineString([.. coordinates]);
                            linestring = linestring.RemoveRepeatedVertices();

                            curves.Add(new S100FC.Topology.Polyline(f.GetObjectID(), name, Convert.ToString(f["code"])!, linestring));
                        }
                    }
                }

                builder = matrix.AddTopologyFeatures(polygons, curves);
            }


            //  Navigational features
            {
                var polygons = new List<S100FC.Topology.Polygon>();

                using (var surface = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => e.GetAliasName().Equals("surface")).GetName())) {
                    queryFilter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) NOT IN ('DEPTHAREA','DREDGEDAREA','LANDAREA','UNSURVEYEDAREA'))";

                    using (var cursor = surface.Search(queryFilter)) {
                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            var shape = (ArcGIS.Core.Geometry.Polygon)f.GetShape();

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            //if (name.Equals("S1799633")) System.Diagnostics.Debugger.Break();

                            var exteriorRing = shape.GetExteriorRing(0);
                            var coordinates = exteriorRing.Parts[0].Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                            var ex = (LineString)factory.CreateLineString([.. coordinates, coordinates[0]]);
                            ex = ex.RemoveRepeatedVertices();

                            if (shape.PartCount > 1) {
                                var interiorRings = new List<LineString>();

                                foreach (var interiorRing in shape.Parts.Skip(1)) {
                                    coordinates = interiorRing.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                                    var linestring = (LineString)factory.CreateLineString([.. coordinates, coordinates[0]]);
                                    linestring = linestring.RemoveRepeatedVertices();
                                    interiorRings.Add(linestring);
                                }

                                polygons.Add(new S100FC.Topology.Polygon(f.GetObjectID(), name, Convert.ToString(f["code"])!, ex, interiorRings.ToArray()));
                            }
                            else {
                                polygons.Add(new S100FC.Topology.Polygon(f.GetObjectID(), name, Convert.ToString(f["code"])!, ex, []));
                            }
                        }
                    }
                }

                var curves = new List<S100FC.Topology.Polyline>();
                var singletons = new List<S100FC.Topology.Polyline>();

                var singletonsFeatures = "''";// "'ROAD','RAILWAY'";  //'NAVIGATIONLINE','RECOMMENDEDTRACK'

                using (var curve = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => e.GetAliasName().Equals("curve")).GetName())) {
                    queryFilter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) NOT IN ('COASTLINE','DEPTHCONTOUR','SHORELINECONSTRUCTION')) AND (upper(code) NOT IN ({singletonsFeatures}))"; //,'NAVIGATIONLINE','RECOMMENDEDTRACK'
                    using (var cursor = curve.Search(queryFilter)) {
                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            var shape = (ArcGIS.Core.Geometry.Polyline)f.GetShape();

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            var coordinates = shape.Points.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.X, segment.Y)).ToArray();

                            var linestring = (LineString)factory.CreateLineString([.. coordinates]);
                            linestring = linestring.RemoveRepeatedVertices();

                            //linestring.Normalize();

                            curves.Add(new S100FC.Topology.Polyline(f.GetObjectID(), name, Convert.ToString(f["code"])!, linestring));
                        }
                    }

                    queryFilter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) IN ({singletonsFeatures}))";
                    using (var cursor = curve.Search(queryFilter)) {
                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            //if (f.GetObjectID() == 44) System.Diagnostics.Debugger.Break();

                            var shape = (ArcGIS.Core.Geometry.Polyline)f.GetShape();

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            var coordinates = shape.Points.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.X, segment.Y)).ToArray();

                            var linestring = (LineString)factory.CreateLineString([.. coordinates]);
                            linestring = linestring.RemoveRepeatedVertices();

                            singletons.Add(new S100FC.Topology.Polyline(f.GetObjectID(), name, Convert.ToString(f["code"])!, linestring));
                        }
                    }
                }

                builder = matrix.AddNavigationalFeatures(polygons, curves);//.AddSingletonFeatures(singletons);
            }

            var result = builder.BuildTopology();

            return result;
        }



        private static Polyline CreateLinearRing(string[] coords, SpatialReference spatialReference) {
            var points = new MapPoint[coords.Length / 2];
            for (int i = 0; i < coords.Length; i += 2) {
                var p = MapPointBuilderEx.CreateMapPoint(
                    double.Parse(coords[i + 1], CultureInfo.InvariantCulture),
                    double.Parse(coords[i + 0], CultureInfo.InvariantCulture),
                    spatialReference);
                points[i / 2] = p;
            }
            return PolylineBuilderEx.CreatePolyline(points, spatialReference);
        }
    }
}