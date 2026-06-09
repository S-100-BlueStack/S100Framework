using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using GeoAPI.Geometries;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Windows.ApplicationModel.Appointments;
using Xunit.Abstractions;
using IO = System.IO;

namespace TestTopology
{
    public class TopologyNetworkTest
    {
        private readonly ITestOutputHelper _output;

        public TopologyNetworkTest(ITestOutputHelper output) {
            this._output = output;
            ArcGIS.Core.Hosting.Host.Initialize();

            foreach (var f in System.IO.Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, $"*topology*.geodatabase*")) {
                if (IO.Path.GetFileName(f).Equals("topology.geodatabase")) continue;
                System.IO.File.Delete(System.IO.Path.GetFullPath(f));
            }
        }


        [Fact]
        public void Test1() {
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

            var fullpath = @"E:\ArcGIS\Projects\DK0040339E\s100ed14.geodatabase";

            var createGeodatabase = () => { return new Geodatabase(new MobileGeodatabaseConnectionPath(new Uri(IO.Path.GetFullPath(fullpath)))); };

            using var geodatabase = createGeodatabase();

            var syntax = geodatabase.GetSQLSyntax();
            var definitions = geodatabase.GetDefinitions<FeatureClassDefinition>();

            //var polygons = new HashSet<(string UID, NetTopologySuite.Geometries.Polygon geometry)>();

            var dictionary = new Dictionary<string, string[]>();
            var polygons = new Dictionary<string, NetTopologySuite.Geometries.Polygon>();

            string[] polygonLayers = ["topo_surface", "surface"];

            foreach (var layer in polygonLayers) {
                using (var surface = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => syntax.ParseTableName(e.GetName()).Item3.Equals(layer)).GetName())) {
                    using var cursor = surface.Search(new QueryFilter {
                        WhereClause = "1=1",
                    }, true);

                    while (cursor.MoveNext()) {
                        var f = (Feature)cursor.Current;
                        var shape = (ArcGIS.Core.Geometry.Polygon)f.GetShape();
                        var name = Convert.ToString(f["UID"])!;

                        var exteriorRing = shape.GetExteriorRing(0);

                        var coordinates = exteriorRing.Parts[0].Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                        var exteriorLineString = factory.CreateLineString([.. coordinates, coordinates[0]]).RemoveRepeatedVertices();
                        if (shape.PartCount > 1) {
                            var interiorRings = new List<LineString>();

                            foreach (var interiorRing in shape.Parts.Skip(1)) {
                                coordinates = interiorRing.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.StartPoint.X, segment.StartPoint.Y)).ToArray();

                                var linestring = factory.CreateLineString([.. coordinates, coordinates[0]]);
                                linestring = linestring.RemoveRepeatedVertices();
                                //linestring.Normalize();
                                linestring = linestring.RemoveCollinearVertices();

                                if (!linestring.IsSelfIntersections())
                                    interiorRings.Add(linestring);
                                else {
                                    foreach (var l in GeometryExtensions.SplitAtSelfIntersections(linestring))
                                        interiorRings.Add(l);
                                }
                            }

                            var polygon = factory.CreatePolygon(factory.CreateLinearRing(exteriorLineString.Coordinates), [.. interiorRings.Select(e => factory.CreateLinearRing(e.Coordinates))]);

                            var key = polygon.ToText();
                            if (!dictionary.ContainsKey(key)) {
                                polygons.Add(key, polygon);
                                dictionary.Add(key, new string[0]);
                            }
                            dictionary[key] = [.. dictionary[key], name];
                        }
                        else {
                            var polygon = factory.CreatePolygon(factory.CreateLinearRing(exteriorLineString.Coordinates), []);

                            var key = polygon.ToText();
                            if (!dictionary.ContainsKey(key)) {
                                polygons.Add(key, polygon);
                                dictionary.Add(key, new string[0]);
                            }
                            dictionary[key] = [.. dictionary[key], name];
                        }
                    }
                }
            }

            //if (dictionary.Any(e => e.Value.Length > 1)) System.Diagnostics.Debugger.Break();

            var network = new PolygonTopologyNetwork(dictionary.Select(e => polygons[e.Key]), factory);
            network.Build();

            var spatialReference = SpatialReferenceBuilder.CreateSpatialReference(4326);

            using var debugInstance = new Geodatabase(new MobileGeodatabaseConnectionPath(new Uri(IO.Path.GetFullPath("topology.geodatabase"))));

            using var polylineFC = debugInstance.OpenDataset<FeatureClass>("main.linestring");
            using var pointFC = debugInstance.OpenDataset<FeatureClass>("main.point");

            using var buffer_linestring = polylineFC.CreateRowBuffer();
            using var buffer_point = pointFC.CreateRowBuffer();

            polylineFC.DeleteRows(new QueryFilter { WhereClause = "1=1" });
            pointFC.DeleteRows(new QueryFilter { WhereClause = "1=1" });



            var edges = network.GetSharedEdges();

            this._output.WriteLine($"Edges: #{edges.Count()}");

            //  Analyzer


            foreach (var (a, b, edge) in edges) {
                var lookupA = dictionary.Single(e => e.Key.Equals(a.ToText()));
                var lookupB = dictionary.Single(e => e.Key.Equals(b.ToText()));

                buffer_linestring["message"] = $"{string.Join(',', lookupA.Value)} {string.Join(',', lookupB.Value)}";

                if (edge is Point point) {
                    buffer_point["shape"] = MapPointBuilderEx.CreateMapPoint(point.X, point.Y, spatialReference);
                    pointFC.CreateRow(buffer_point);
                }
                else if (edge is LineString lineString) {
                    buffer_linestring["shape"] = Shared.ConvertToArcGISPolyline(lineString, spatialReference);
                    polylineFC.CreateRow(buffer_linestring);
                }
                else if (edge is MultiLineString multiLineString) {
                    foreach (LineString m in multiLineString) {
                        buffer_linestring["shape"] = Shared.ConvertToArcGISPolyline(m, spatialReference);
                        polylineFC.CreateRow(buffer_linestring);
                    }
                }
                else if (edge is GeometryCollection geometryCollection) {
                    foreach (var geom in geometryCollection) {
                        if (geom is LineString gLineString) {
                            buffer_linestring["shape"] = Shared.ConvertToArcGISPolyline(gLineString, spatialReference);
                            polylineFC.CreateRow(buffer_linestring);
                        }
                        else if (geom is Point gPoint) {
                            buffer_point["shape"] = MapPointBuilderEx.CreateMapPoint(gPoint.X, gPoint.Y, spatialReference);
                            pointFC.CreateRow(buffer_point);
                        }
                        else
                            System.Diagnostics.Debugger.Break();
                    }
                }
                else
                    System.Diagnostics.Debugger.Break();
            }
        }
    }

    public class PolygonTopologyNetwork
    {
        private readonly GeometryFactory _factory;
        private readonly List<NetTopologySuite.Geometries.Polygon> _polygons;

        // The noded combined geometry — shared edges appear only once
        public NetTopologySuite.Geometries.Geometry? NodedGeometry { get; private set; } = default;

        public PolygonTopologyNetwork(IEnumerable<NetTopologySuite.Geometries.Polygon> polygons, NetTopologySuite.Geometries.GeometryFactory? factory = null) {
            _factory = factory ?? NtsGeometryServices.Instance.CreateGeometryFactory();
            _polygons = polygons.ToList();
        }

        public void Build() {
            // Step 1: Union all polygons — this nodes shared edges
            // CascadedPolygonUnion is O(n log n) and handles large sets well
            var union = NetTopologySuite.Operation.Union
                            .CascadedPolygonUnion.Union([.. _polygons]);
            NodedGeometry = union;
        }

        // Find all polygons adjacent to a given polygon (share a boundary segment)
        public IEnumerable<NetTopologySuite.Geometries.Polygon> GetAdjacentPolygons(NetTopologySuite.Geometries.Polygon query) {
            return _polygons
                .Where(p => !p.EqualsTopologically(query)
                            && p.Touches(query));  // Touches = shared boundary, no overlap
        }

        // Find all shared edges between polygon pairs
        public IEnumerable<(NetTopologySuite.Geometries.Polygon A, NetTopologySuite.Geometries.Polygon B, NetTopologySuite.Geometries.Geometry SharedBoundary)>
            GetSharedEdges() {
            var strtree = new NetTopologySuite.Index.Strtree.STRtree<NetTopologySuite.Geometries.Polygon>();
            foreach (var p in _polygons)
                strtree.Insert(p.EnvelopeInternal, p);

            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < _polygons.Count; i++) {
                var candidates = strtree.Query(_polygons[i].EnvelopeInternal)
                                        .Cast<NetTopologySuite.Geometries.Polygon>();
                foreach (var candidate in candidates) {
                    int j = _polygons.IndexOf(candidate);
                    if (j <= i) continue;

                    var key = (Math.Min(i, j), Math.Max(i, j));
                    if (!seen.Add(key)) continue;

                    if (_polygons[i].Touches(candidate)) {
                        var shared = _polygons[i].Intersection(candidate);
                        yield return (_polygons[i], candidate, shared);
                    }
                }
            }
        }
    }
}
