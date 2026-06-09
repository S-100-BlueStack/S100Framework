using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using GeoAPI.Geometries;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using S100FC.Topology;
using S100Framework.Topology;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;
using IO = System.IO;

namespace TestTopology
{
    public class ReloadedTest
    {
        private readonly ITestOutputHelper _output;

        public ReloadedTest(ITestOutputHelper output) {
            this._output = output;
            ArcGIS.Core.Hosting.Host.Initialize();

            foreach (var f in System.IO.Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, $"*topology*.geodatabase*")) {
                if (IO.Path.GetFileName(f).Equals("topology.geodatabase")) continue;
                System.IO.File.Delete(System.IO.Path.GetFullPath(f));
            }
        }

        [Fact]
        public void Test1() {
            var reloaded = Reloaded.CreateMatrix((args) => {

            });

            var builder = LoadGeodatabase((ITopologyBuilder)reloaded, @"E:\ArcGIS\Projects\DK0040339E\s100ed14.geodatabase", reloaded.Factory);

            builder.BuildTopology();
        }

        private static ITopologyBuilder LoadGeodatabase(ITopologyBuilder topologyBuilder, string fullpath, GeometryFactory factory) {
            var createGeodatabase = () => { return new Geodatabase(new MobileGeodatabaseConnectionPath(new Uri(IO.Path.GetFullPath(fullpath)))); };

            using var geodatabase = createGeodatabase();

            var syntax = geodatabase.GetSQLSyntax();
            var definitions = geodatabase.GetDefinitions<FeatureClassDefinition>();

            //var polygons = new HashSet<(string UID, NetTopologySuite.Geometries.Polygon geometry)>();

            var dictionary = new Dictionary<string, string[]>();
            var polylines = new Dictionary<string, NetTopologySuite.Geometries.LineString>();
            var polygons = new Dictionary<string, NetTopologySuite.Geometries.Polygon>();

            string[] polylineLayers = ["topo_curve", "curve"];
            string[] polygonLayers = ["topo_surface", "surface"];

            var instancePolylines = new Dictionary<string, S100FC.Topology.Polyline[]>();
            var instancePolygons = new Dictionary<string, S100FC.Topology.Polygon[]>();

            foreach (var layer in polygonLayers) {
                if (!instancePolygons.ContainsKey(layer))
                    instancePolygons.Add(layer, []);

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

                            instancePolygons[layer] = [.. instancePolygons[layer], new S100FC.Topology.Polygon(f.GetObjectID(), name, Convert.ToString(f["code"])!, exteriorLineString, interiorRings.ToArray())];
                        }
                        else {
                            instancePolygons[layer] = [.. instancePolygons[layer], new S100FC.Topology.Polygon(f.GetObjectID(), name, Convert.ToString(f["code"])!, exteriorLineString, [])];
                        }
                    }
                }
            }

            foreach (var layer in polylineLayers) {
                if (!instancePolylines.ContainsKey(layer))
                    instancePolylines.Add(layer, []);

                using (var surface = geodatabase.OpenDataset<FeatureClass>(definitions.Single(e => syntax.ParseTableName(e.GetName()).Item3.Equals(layer)).GetName())) {
                    using var cursor = surface.Search(new QueryFilter {
                        WhereClause = "1=1",
                    }, true);

                    while (cursor.MoveNext()) {
                        var f = (Feature)cursor.Current;
                        var shape = (ArcGIS.Core.Geometry.Polyline)f.GetShape();
                        var name = Convert.ToString(f["UID"])!;

                        for (int i = 0; i < shape.PartCount; i++) {
                            var p = PolylineBuilderEx.CreatePolyline(shape.Parts[i]);

                            var coordinates = p.Points.Select(segment => new NetTopologySuite.Geometries.Coordinate(segment.X, segment.Y)).ToArray();

                            var linestring = factory.CreateLineString([.. coordinates]);
                            linestring = linestring.RemoveRepeatedVertices();

                            instancePolylines[layer] = [.. instancePolylines[layer], new S100FC.Topology.Polyline(f.GetObjectID(), name, Convert.ToString(f["code"])!, linestring, name)];
                        }
                    }
                }
            }

            topologyBuilder.AddTopologyFeatures(instancePolygons["topo_surface"], instancePolylines["topo_curve"]);
            topologyBuilder.AddNavigationalFeatures(instancePolygons["surface"], instancePolylines["curve"]);

            return topologyBuilder;
        }
    }
}
