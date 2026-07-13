//#define SKIN_OF_THE_EARTH_ONLY

using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Topology;
using ArcGIS.Core.SystemCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Noding;
using NetTopologySuite.Noding.Snapround;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Operation.Valid;
using System.Globalization;


namespace ArcGIS.Core.Data
{
    public static class Helper
    {
        public static Geodatabase OpenGeodatabase(string workspacePath, string portalUrl = "", string portalUser = "", string portalPassword = "", bool forceSignOut = true) {
            if (workspacePath.EndsWith(".geodatabase", StringComparison.InvariantCultureIgnoreCase))
                return new Geodatabase(new MobileGeodatabaseConnectionPath(new Uri(workspacePath)));
            else if (workspacePath.EndsWith(".gdb", StringComparison.InvariantCultureIgnoreCase))
                return new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspacePath)));
            else if (workspacePath.EndsWith(".sde", StringComparison.InvariantCultureIgnoreCase))
                return new Geodatabase(new DatabaseConnectionFile(new Uri(workspacePath)));
            else if (workspacePath.StartsWith("memory", StringComparison.InvariantCultureIgnoreCase))
                return new Geodatabase(new MemoryConnectionProperties(workspacePath));
            else if (workspacePath.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                return OpenFeatureService(workspacePath, portalUrl, portalUser, portalPassword, forceSignOut);
            else
                throw new ArgumentException("Unrecognized workspace type: " + workspacePath);
        }

        public static Geodatabase OpenFeatureService(string featureServiceUrl, string portalUrl, string portalUser, string portalPassword, bool forceSignOut = true) {
            var arcGisSignOn = ArcGISSignOn.Instance;
            var portalUri = new Uri(portalUrl);
            var workspaceUri = new Uri(featureServiceUrl);
            if (forceSignOut && arcGisSignOn.IsSignedOn(portalUri))
                arcGisSignOn.SignOut(portalUri);
            if (!arcGisSignOn.IsSignedOn(portalUri))
                arcGisSignOn.SignInWithCredentials(portalUri, portalUser, portalPassword, out var referer, out var token);

            return new Geodatabase(new ServiceConnectionProperties(workspaceUri));
        }
    }
}

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

        const double gridSizeMeters = 0.08;
        const double scale = 111320.0 / gridSizeMeters; // ≈ 1,391,500

        //static readonly PrecisionModel precisionModel = new PrecisionModel(100000);
        //static readonly PrecisionModel precisionModel = new PrecisionModel(scale);
        static readonly PrecisionModel precisionModel = new PrecisionModel(1000000);
        //static readonly PrecisionModel precisionModel = new PrecisionModel(2000000);
        //static readonly PrecisionModel precisionModel = new PrecisionModel(10000000);

        static readonly GeometryFactory factory = new GeometryFactory(precisionModel, srid: 4326); // Or PrecisionModels.Floating        

        public static (S100FC.Topology.IMatrix matrix, IDictionary<string, string> mapper) BuildTopology(this Geodatabase geodatabase, QueryFilter? queryFilter = default, Action<int, ICollection<(LineString lineString, string message)>>? interceptor = default) {
            var syntax = geodatabase.GetSQLSyntax();

            QueryFilter[] filters = [];
            Geometry? filterGeometry = default;

            if (queryFilter is SpatialQueryFilter spatial) {
                filterGeometry = spatial.FilterGeometry;

                var contains = new SpatialQueryFilter {
                    FilterGeometry = spatial.FilterGeometry,
                    ObjectIDs = spatial.ObjectIDs,
                    Offset = spatial.Offset,
                    OutputSpatialReference = spatial.OutputSpatialReference,
                    PostfixClause = spatial.PostfixClause,
                    PrefixClause = spatial.PrefixClause,
                    RowCount = spatial.RowCount,
                    SearchOrder = spatial.SearchOrder,
                    SpatialRelationship = spatial.SpatialRelationship,
                    SpatialRelationshipDescription = S100FC.Topology.Matrix.DE9IM_Contains,
                    SubFields = spatial.SubFields,
                    WhereClause = $"({spatial.WhereClause})",
                };

                var crosses = new SpatialQueryFilter {
                    FilterGeometry = spatial.FilterGeometry,
                    ObjectIDs = spatial.ObjectIDs,
                    Offset = spatial.Offset,
                    OutputSpatialReference = spatial.OutputSpatialReference,
                    PostfixClause = spatial.PostfixClause,
                    PrefixClause = spatial.PrefixClause,
                    RowCount = spatial.RowCount,
                    SearchOrder = spatial.SearchOrder,
                    SpatialRelationship = spatial.SpatialRelationship,
                    SpatialRelationshipDescription = S100FC.Topology.Matrix.DE9IM_Crosses,
                    SubFields = spatial.SubFields,
                    WhereClause = $"({spatial.WhereClause})",
                };

                filters = [contains, crosses];
            }
            else if (queryFilter is not null) {
                filters = [queryFilter];
            }
            else {
                queryFilter = new QueryFilter {
                    WhereClause = "upper(ps) = 'S-101'",
                };
                filters = [queryFilter];
            }

            var whereClause = queryFilter.WhereClause;
            var prefix = queryFilter.PrefixClause;

            S100FC.Topology.Matrix.Factory = S100FC.Topology.Reloaded.Factory = factory;

            var definitions = geodatabase.GetDefinitions<FeatureClassDefinition>();


            //var matrix = S100FC.Topology.Matrix.CreateMatrix(interceptor);
            var matrix = S100FC.Topology.Reloaded.CreateMatrix(interceptor);            

            S100FC.Topology.ITopologyBuilder? builder = default;

            var clipGeometry = (Geometry g) => {
                return g;
            };

            if (filterGeometry is not null) {
                clipGeometry = (Geometry g) => {
                    if (g is Polyline polyline) return polyline;

                    if (GeometryEngine.Instance.Disjoint(g, filterGeometry)) return g;

                    if (!GeometryEngine.Instance.Relate(g, filterGeometry, S100FC.Topology.Matrix.DE9IM_Crosses)) return g;

                    var difference = GeometryEngine.Instance.Intersection(g, filterGeometry);

                    if (difference is Polygon polygon) {
                        if (polygon.ExteriorRingCount > 1) {
                            Polygon[] polygons = [];
                            ReadOnlySegmentCollection[] segments = [polygon.Parts[0]];
                            for (int i = 1; i < polygon.PartCount; i++) {
                                var p = PolygonBuilderEx.CreatePolygon(polygon.Parts[i]);
                                if (p.Area < 0)
                                    segments = [.. segments, polygon.Parts[i]];
                                else {
                                    var _ = PolygonBuilderEx.CreatePolygon(segments);
                                    polygons = [.. polygons, _];
                                    segments = [polygon.Parts[i]];
                                }
                            }
                            if (segments.Any()) {
                                var _ = PolygonBuilderEx.CreatePolygon(segments);
                                polygons = [.. polygons, _];
                            }
                            return g = PolygonBuilderEx.CreatePolygon(polygons);
                        }
                        else {
                            return polygon;
                        }
                    }
                    else
                        System.Diagnostics.Debugger.Break();

                    return g;
                };
            }

            var mapper = new Dictionary<string, string>();

            //  Skin of the Earth
            {
                var polygons = new List<S100FC.Topology.Polygon>();

                using (var surface = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => syntax.ParseTableName(e.GetName()).Item3.Equals("surface")).GetName())) {
                    foreach (var filter in filters) {
                        filter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) IN ('DEPTHAREA','DREDGEDAREA','LANDAREA','UNSURVEYEDAREA','SHORELINECONSTRUCTION'))";

                        using var cursor = surface.Search(filter);

                        var lookup = polygons.ToLookup(e => e.ObjectId, e => e);

                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            if (lookup.Contains(f.GetObjectID())) continue;

                            var shape = (ArcGIS.Core.Geometry.Polygon)f.GetShape();

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            shape = (Polygon)clipGeometry(shape);
                            if (shape.IsEmpty) continue;

                            var exteriorRing = shape.GetExteriorRing(0);
                            var coordinates = exteriorRing.Parts[0].Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                            var ex = factory.CreateLinearRing([.. coordinates, coordinates[0]]);
                            ex = (LinearRing)matrix.Reducer.Reduce(ex);
                            //ex = ex.RemoveRepeatedVertices().RemoveCollinearVertices();
                            //ex.Normalize();

                            if (shape.PartCount > 1) {
                                var interiorRings = new List<LinearRing>();

                                foreach (var interiorRing in shape.Parts.Skip(1)) {
                                    coordinates = interiorRing.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                                    var linestring = factory.CreateLinearRing([.. coordinates, coordinates[0]]);
                                    linestring = (LinearRing)matrix.Reducer.Reduce(linestring);

                                    if (!linestring.IsSelfIntersections())
                                        interiorRings.Add(linestring);
                                    else {
                                        foreach (var l in SplitAtSelfIntersections(linestring))                                            
                                            interiorRings.Add(l.Factory.CreateLinearRing(l.Coordinates));
                                    }
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

                using (var curve = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => syntax.ParseTableName(e.GetName()).Item3.Equals("curve")).GetName())) {
                    foreach (var filter in filters) {
                        filter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) IN ('COASTLINE','DEPTHCONTOUR','SHORELINECONSTRUCTION'))";

                        using var cursor = curve.Search(filter);

                        var lookup = curves.ToLookup(e => e.ObjectId, e => e);

                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            if (lookup.Contains(f.GetObjectID())) continue;

                            var shape = (Polyline)f.GetShape();

                            shape = (Polyline)clipGeometry(shape);
                            if (shape.IsEmpty) continue;

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            //if ("F10500070853".Equals(name)) System.Diagnostics.Debugger.Break();

                            LineString[] parts = [];
                            foreach (var part in shape.Parts) {
                                var p = PolylineBuilderEx.CreatePolyline(part);

                                var coordinates = p.Points.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.X, segment.Y)).ToArray();

                                var linestring = factory.CreateLineString([.. coordinates]);
                                linestring = (LineString)matrix.Reducer.Reduce(linestring);

                                if (!linestring.IsSelfIntersections())
                                    parts = [.. parts, linestring];
                                else {
                                    foreach (var l in SplitAtSelfIntersections(linestring))
                                        parts = [.. parts, l];
                                }
                            }
                            if (parts.Length == 1) {
                                curves.Add(new S100FC.Topology.Polyline(f.GetObjectID(), name, Convert.ToString(f["code"])!, parts[0], name));
                            }
                            else {
                                for (int i = 0; i < parts.Length; i++) {
                                    curves.Add(new S100FC.Topology.Polyline(f.GetObjectID(), $"{name}:{i}", Convert.ToString(f["code"])!, parts[i], name));
                                    mapper.Add($"{name}:{i}", name);
                                }
                            }
                        }
                    }
                }

                builder = matrix.AddTopologyFeatures(polygons, curves);
            }

            //  Navigational features
            {
                //string[] testFeatures = ["DataCoverage", "SoundingDatum", "VerticalDatum", "NavigationalSystemOfMarks"];
                string[] testFeatures = ["DataCoverage"];

                var polygons = new List<S100FC.Topology.Polygon>();

                using (var surface = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => syntax.ParseTableName(e.GetName()).Item3.Equals("surface")).GetName())) {
                    //queryFilter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) NOT IN ('DEPTHAREA','DREDGEDAREA','LANDAREA','UNSURVEYEDAREA'))";

                    foreach (var filter in filters) {
                        filter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) NOT IN ('DEPTHAREA','DREDGEDAREA','LANDAREA','UNSURVEYEDAREA','SHORELINECONSTRUCTION'))";

                        using var cursor = surface.Search(filter);

                        var lookup = polygons.ToLookup(e => e.ObjectId, e => e);

                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            if (lookup.Contains(f.GetObjectID())) continue;

                            //if (Convert.ToString(f["code"]).Equals("SoundingDatum")) System.Diagnostics.Debugger.Break();

#if SKIN_OF_THE_EARTH_ONLY
                            if (!testFeatures.Contains(Convert.ToString(f["code"]))) continue;
#endif
                            var shape = (ArcGIS.Core.Geometry.Polygon)f.GetShape();

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            if("F10400819365".Equals(name)) System.Diagnostics.Debugger.Break();

                            shape = (Polygon)clipGeometry(shape);
                            if (shape.IsEmpty) continue;

                            var exteriorRing = shape.GetExteriorRing(0);
                            var coordinates = exteriorRing.Parts[0].Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                            //for (int _ = 0; _ < coordinates.Length; _++)
                            //    coordinates[_] = SnapToGrid(coordinates[_]);

                            var ex = factory.CreateLinearRing([.. coordinates, coordinates[0]]);
                            ex = (LinearRing)matrix.Reducer.Reduce(ex);
                            //ex = ex.RemoveRepeatedVertices().RemoveCollinearVertices();

                            if (shape.PartCount > 1) {
                                var interiorRings = new List<LineString>();

                                foreach (var interiorRing in shape.Parts.Skip(1)) {
                                    coordinates = interiorRing.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                                    var linestring = factory.CreateLinearRing([.. coordinates, coordinates[0]]);
                                    linestring = (LinearRing)matrix.Reducer.Reduce(linestring);

                                    if (!linestring.IsSelfIntersections())
                                        interiorRings.Add(linestring);
                                    else {
                                        foreach (var l in SplitAtSelfIntersections(linestring))
                                            interiorRings.Add(l.Factory.CreateLinearRing(l.Coordinates));
                                    }
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

                using (var curve = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => syntax.ParseTableName(e.GetName()).Item3.Equals("curve")).GetName())) {
                    foreach (var filter in filters) {
                        filter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) NOT IN ('COASTLINE','DEPTHCONTOUR','SHORELINECONSTRUCTION')) AND (upper(code) NOT IN ({singletonsFeatures}))"; //,'NAVIGATIONLINE','RECOMMENDEDTRACK'

                        using var cursor = curve.Search(filter);

                        var lookup = curves.ToLookup(e => e.ObjectId, e => e);

                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            if (lookup.Contains(f.GetObjectID())) continue;

#if SKIN_OF_THE_EARTH_ONLY
                            continue;
#endif

                            var shape = (Polyline)f.GetShape();

                            shape = (Polyline)clipGeometry(shape);
                            if (shape.IsEmpty) continue;

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            LineString[] parts = [];
                            foreach (var part in shape.Parts) {
                                var p = PolylineBuilderEx.CreatePolyline(part);

                                var coordinates = p.Points.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.X, segment.Y)).ToArray();

                                var linestring = factory.CreateLineString([.. coordinates]);
                                linestring = (LineString)matrix.Reducer.Reduce(linestring);

                                if (!linestring.IsSelfIntersections())
                                    parts = [.. parts, linestring];
                                else {
                                    foreach (var l in SplitAtSelfIntersections(linestring))
                                        parts = [.. parts, l];
                                }
                            }
                            if (parts.Length == 1) {
                                curves.Add(new S100FC.Topology.Polyline(f.GetObjectID(), name, Convert.ToString(f["code"])!, parts[0], name));
                            }
                            else {
                                for (int i = 0; i < parts.Length; i++) {
                                    curves.Add(new S100FC.Topology.Polyline(f.GetObjectID(), $"{name}:{i}", Convert.ToString(f["code"])!, parts[i], name));
                                    mapper.Add($"{name}:{i}", name);
                                }
                            }
                        }
                    }
#if Singletons
                    foreach (var filter in filters) {
                        filter.WhereClause = (!string.IsNullOrEmpty(whereClause) ? $"{whereClause} AND " : "") + $"(upper(code) IN ({singletonsFeatures}))";

                        using var cursor = curve.Search(filter);

                        var lookup = singletons.ToLookup(e => e.ObjectId, e => e);

                        while (cursor.MoveNext()) {
                            var f = (Feature)cursor.Current;

                            if (lookup.Contains(f.GetObjectID())) continue;

                            //if (f.GetObjectID() == 44) System.Diagnostics.Debugger.Break();

                            var shape = (Polyline)f.GetShape();

                            shape = (Polyline)clipGeometry(shape);

                            var name = Convert.ToString(f["UID"]);
                            if (string.IsNullOrEmpty(name))
                                name = string.Empty;

                            //var coordinates = shape.Points.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.X, segment.Y)).ToArray();

                            //var linestring = factory.CreateLineString([.. coordinates]);
                            //linestring = linestring.RemoveRepeatedVertices();

                            //singletons.Add(new S100FC.Topology.Polyline(f.GetObjectID(), name, Convert.ToString(f["code"])!, linestring));
                            for (int i = 0; i < shape.PartCount; i++) {
                                var p = PolylineBuilderEx.CreatePolyline(shape.Parts[i]);

                                var coordinates = p.Points.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.X, segment.Y)).ToArray();

                                var linestring = factory.CreateLineString([.. coordinates]);
                                linestring = linestring.RemoveRepeatedVertices();

                                singletons.Add(new S100FC.Topology.Polyline(f.GetObjectID(), $"{name}:p{i}", Convert.ToString(f["code"])!, linestring, name));                                
                            }
                        }
                    }
#endif
                }

                builder = matrix.AddNavigationalFeatures(polygons, curves);//.AddSingletonFeatures(singletons);
            }

            var result = builder.BuildTopology();            

            //interceptor?.Invoke(6001, result.Curves.Select(e => (e.LineString, $"{e.Id}")).ToArray());


            return (result, mapper);
        }

        private const double _snapTolerance = 0.000000001;

        private static Coordinate SnapToGrid(Coordinate c) {
            double inv = 1.0 / _snapTolerance;
            double x = Math.Round(c.X * inv) / inv;
            double y = Math.Round(c.Y * inv) / inv;

            // Preserve Z if present
            return double.IsNaN(c.Z)
                ? new Coordinate(x, y)
                : new CoordinateZ(x, y, c.Z);
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


        public static bool IsSelfIntersections(this LineString lineString) {
            var locations = new List<Coordinate>();

            // Use IsValidOp which reports exact error locations
            var validOp = new IsValidOp(lineString);

            if (!validOp.IsValid) {
                var error = validOp.ValidationError;
                if (error != null)
                    locations.Add(error.Coordinate);
            }

            // Also check IsSimple for non-simple (self-touching) cases
            bool isSelfIntersecting = !lineString.IsSimple || locations.Any();

            return isSelfIntersecting;
            //return (isSelfIntersecting, locations);
        }

        public static List<LineString> SplitAtSelfIntersections(LineString lineString) {
            var pm = lineString.Factory.PrecisionModel;

            // 1. Node the linestring — inserts split points at every intersection
            var noder = new SnapRoundingNoder(pm);
            var nodedSegments = NodeLineString(lineString, noder);

            // 2. Reassemble noded segments into a geometry
            var lines = nodedSegments
                .Cast<NodedSegmentString>()
                .Select(s => lineString.Factory.CreateLineString(s.Coordinates))
                .ToArray();

            // 3. Use LineMerger to produce clean, non-overlapping LineStrings
            var merger = new LineMerger();
            merger.Add(lines);

            return merger.GetMergedLineStrings()
                .Cast<LineString>()                
                .ToList();
        }

        private static IList<ISegmentString> NodeLineString(
            LineString lineString,
            INoder noder) {
            var segmentString = new NodedSegmentString(
                lineString.Coordinates, null);

            var segments = new List<ISegmentString> { segmentString };
            noder.ComputeNodes(segments);

            return noder.GetNodedSubstrings();
        }




#if null
        public static class LineStringSplitter
        {
            /// <summary>
            /// Splits a self-intersecting LineString into non-self-intersecting parts.
            /// </summary>
            public static List<LineString> Split(LineString lineString) {
                var intersections = GetSelfIntersections(lineString);

                if (intersections.Count == 0)
                    return new List<LineString> { lineString };

                // Insert intersection points into the coordinate sequence
                var densifiedCoords = InsertSplitPoints(
                    lineString.Coordinates, intersections);

                // Split the coordinate array at the intersection points
                return SplitCoordinatesAtPoints(
                    densifiedCoords, intersections, lineString.Factory);
            }

            // -------------------------------------------------------------------------
            // Step 1: Find all self-intersection coordinates
            // -------------------------------------------------------------------------
            private static List<NetTopologySuite.Geometries.Coordinate> GetSelfIntersections(LineString lineString) {
                var intersections = new List<NetTopologySuite.Geometries.Coordinate>();
                var coords = lineString.Coordinates;
                var intersector = new RobustLineIntersector();

                for (int i = 0; i < coords.Length - 1; i++) {
                    for (int j = i + 2; j < coords.Length - 1; j++) {
                        if (lineString.IsClosed && i == 0 && j == coords.Length - 2)
                            continue;

                        intersector.ComputeIntersection(
                            coords[i], coords[i + 1],
                            coords[j], coords[j + 1]);

                        if (intersector.HasIntersection) {
                            for (int k = 0; k < intersector.IntersectionNum; k++)
                                intersections.Add(intersector.GetIntersection(k));
                        }
                    }
                }

                return intersections
                    .Distinct(new CoordinateEqualityComparer())
                    .ToList();
            }

            // -------------------------------------------------------------------------
            // Step 2: Walk the coordinate sequence and insert split points where
            //         an intersection falls along a segment
            // -------------------------------------------------------------------------
            private static List<NetTopologySuite.Geometries.Coordinate> InsertSplitPoints(
                NetTopologySuite.Geometries.Coordinate[] original,
                List<NetTopologySuite.Geometries.Coordinate> splitPoints) {
                var result = new List<NetTopologySuite.Geometries.Coordinate>();

                for (int i = 0; i < original.Length - 1; i++) {
                    result.Add(original[i]);

                    var segStart = original[i];
                    var segEnd = original[i + 1];

                    // Find any split points that lie on this segment
                    var onSegment = splitPoints
                        .Where(p => LiesOnSegment(p, segStart, segEnd)
                                 && !p.Equals2D(segStart)
                                 && !p.Equals2D(segEnd))
                        .OrderBy(p => segStart.Distance(p)) // insert in order along segment
                        .ToList();

                    result.AddRange(onSegment);
                }

                result.Add(original[^1]); // add final coordinate
                return result;
            }

            // -------------------------------------------------------------------------
            // Step 3: Walk the densified sequence and cut at each split point
            // -------------------------------------------------------------------------
            private static List<LineString> SplitCoordinatesAtPoints(
                List<NetTopologySuite.Geometries.Coordinate> coords,
                List<NetTopologySuite.Geometries.Coordinate> splitPoints,
                GeometryFactory factory) {
                var result = new List<LineString>();
                var current = new List<NetTopologySuite.Geometries.Coordinate>();

                foreach (var coord in coords) {
                    current.Add(coord);

                    // If this coord is a split point (and we have enough for a segment),
                    // close off the current part and start a new one
                    bool isSplitPoint = splitPoints.Any(p => p.Equals2D(coord));

                    if (isSplitPoint && current.Count >= 2) {
                        result.Add(factory.CreateLineString(current.ToArray()));
                        current = new List<NetTopologySuite.Geometries.Coordinate> { coord }; // carry forward as new start
                    }
                }

                // Add any remaining coordinates as the final segment
                if (current.Count >= 2)
                    result.Add(factory.CreateLineString(current.ToArray()));

                return result;
            }

            // -------------------------------------------------------------------------
            // Helpers
            // -------------------------------------------------------------------------
            private static bool LiesOnSegment(NetTopologySuite.Geometries.Coordinate p, NetTopologySuite.Geometries.Coordinate a, NetTopologySuite.Geometries.Coordinate b) {
                // Check collinearity and that p is within the bounding box of a→b
                return CGAlgorithms.IsOnLine(p, new[] { a, b });
            }
        }

        public class CoordinateEqualityComparer : IEqualityComparer<NetTopologySuite.Geometries.Coordinate>
        {
            public bool Equals(NetTopologySuite.Geometries.Coordinate x, NetTopologySuite.Geometries.Coordinate y) =>
                x.X == y.X && x.Y == y.Y;

            public int GetHashCode(NetTopologySuite.Geometries.Coordinate c) =>
                HashCode.Combine(c.X, c.Y);
        }
#endif
    }
}