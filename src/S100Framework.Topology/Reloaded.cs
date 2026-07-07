using NetTopologySuite.Geometries;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestTopology")]

namespace S100FC.Topology
{
    using GeoAPI.Geometries;
    using NetTopologySuite.Algorithm.Match;
    using NetTopologySuite.Operation.Linemerge;
    using NetTopologySuite.Precision;
    using S100Framework.Topology;
    using S100Framework.Topology.Internal;

    public interface IMatrixReloaded : IMatrix
    {
        GeometryFactory Factory { get; }
    }

    public class Reloaded : ITopologyBuilder, IMatrixReloaded
    {
        public class LineStringComparer : IEqualityComparer<LineString>
        {
            public bool Equals(LineString? a, LineString? b) {
                if (a is null || b is null) return a is null && b is null;
                return a.Equals(b);
            }

            public int GetHashCode(LineString e) => (int)System.IO.Hashing.XxHash32.HashToUInt32(e.AsBinary());
        }

        private class Surface
        {
            public int Id { get; init; }

            public required string Exterior { get; init; }

            public string[]? Interior { get; set; } = default;

            public string Name => $"S{this.Id}";
        }

        public static GeometryFactory? Factory { get; set; } = default; // new GeometryFactory(new PrecisionModel(10000000), srid: 4326); // Or PrecisionModels.Floating

        private Action<int, ICollection<(LineString lineString, string message)>>? _interceptor;

        private readonly MixedTopologyNetwork _mixedTopologyNetwork;

        private readonly TopologyBuilder _topologyBuilder;

        private readonly Dictionary<string, PolygonSource> _featureMapperPolygons = [];
        private readonly Dictionary<string, int> _featureMapperLineStrings = [];

        private readonly Dictionary<string, string> _mapping = [];

        protected Reloaded() {
            //  Protected default constructor            
            this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory!, snapTolerance: Reloaded.Factory!.PrecisionModel.GridSize);

            this._topologyBuilder = new TopologyBuilder();
        }

        public static ITopologyBuilder CreateMatrix(Action<int, ICollection<(LineString lineString, string message)>>? interceptor = default) {
            return new Reloaded() {
                _interceptor = interceptor,
            };
        }

        GeometryFactory IMatrixReloaded.Factory => Reloaded.Factory!;

        ITopologyBuilder ITopologyBuilder.AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddTopologyFeatures(surfaces, curves, true);

        ITopologyBuilder ITopologyBuilder.AddNavigationalFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddTopologyFeatures(surfaces, curves, false);

        public GeometryPrecisionReducer Reducer => new GeometryPrecisionReducer(Factory!.PrecisionModel) {
            Pointwise = true,
            RemoveCollapsedComponents = true,
        };

        IMatrix ITopologyBuilder.BuildTopology() {
            this._mapping.Clear();
            this._mixedTopologyNetwork.Build();

            var featureRefs = new Dictionary<ulong, FeatureRef>();
            var featureRefs2Reverse = new Dictionary<ulong, ulong>();

            var sourceId2FeatureRef = new Dictionary<int, ulong>();

            var (allEdges, sourceRefs) = this._mixedTopologyNetwork.BuildEdgeIndex();

            if (System.Diagnostics.Debugger.IsAttached) {
                goto __skip__test;
                string[] test_EdgesText = [];
                LineString[] test_Edges = [];

                for (int j = 0; j < allEdges.Count; j++) {
                    var e = allEdges[j];
                    var txt = e.Geometry.ToText();
                    if (test_EdgesText.Contains(txt)) continue;

                    for (int i = 0; i < test_Edges.Length; i++) {
                        if (e.Geometry.Overlaps(test_Edges[i])) {
                            var existing = test_Edges[i];
                            var overlapping = e.Geometry;

                            this._interceptor?.Invoke(100, [(existing, $"{existing.ToText()}"), (overlapping, $"{overlapping.ToText()}")]);
                            System.Diagnostics.Debugger.Break();
                        }
                    }
                    test_EdgesText = [.. test_EdgesText, txt];
                    test_Edges = [.. test_Edges, e.Geometry];
                }

                test_EdgesText = [];
                test_Edges = [];

                Dictionary<LineString, int> sourceMapper = new();

                foreach (var sourceId in this._mixedTopologyNetwork.Sources) {
                    var edges = sourceRefs[sourceId];
                    if (!edges.Any()) continue;
                    if (edges.Count > 1 && edges[0].Geometry.Equals(edges[^1].Geometry))
                        edges = edges[..^1];

                    foreach (var e in edges) {
                        var txt = e.Geometry.ToText();
                        if (test_EdgesText.Contains(txt)) continue;

                        for (int i = 0; i < test_Edges.Length; i++) {
                            if (e.Geometry.Overlaps(test_Edges[i])) {
                                var existing = test_Edges[i];
                                var overlapping = e.Geometry;

                                var ee = sourceMapper[existing];

                                if (sourceId == ee) {
                                    this._interceptor?.Invoke(100, [.. edges.Select(e => (e.Geometry, $"{e.Geometry.ToText()}"))]);
                                }

                                this._interceptor?.Invoke(100, [(existing, $"{existing.ToText()}"), (overlapping, $"{overlapping.ToText()}")]);
                                System.Diagnostics.Debugger.Break();
                            }
                        }
                        test_EdgesText = [.. test_EdgesText, txt];
                        test_Edges = [.. test_Edges, e.Geometry];

                        sourceMapper.Add(e.Geometry, sourceId);
                    }
                }
            __skip__test:
                ;
            }
            ulong[] featureRefUsed = [];

            foreach (var sourceId in this._mixedTopologyNetwork.Sources) {
                var edges = sourceRefs[sourceId];
                if (!edges.Any()) continue;

                foreach (var e in edges) {
                    var hashGeometry = System.IO.Hashing.XxHash32.HashToUInt32(e.Geometry.AsBinary());
                    if (!featureRefs.ContainsKey(hashGeometry)) {
                        var featureRef1 = new FeatureRef {
                            Id = hashGeometry,
                            Reverse = false,
                        };
                        featureRefs.Add(hashGeometry, featureRef1);

                        var curve = new CurveFeature(e.Geometry, hashGeometry);
                        this._curves.Add(featureRef1.Id, curve);

                        //if (curve.Id == 1471310191) System.Diagnostics.Debugger.Break();
                    }

                    var hashGeometryReverse = System.IO.Hashing.XxHash32.HashToUInt32(e.Geometry.Reverse().AsBinary());
                    if (!featureRefs.ContainsKey(hashGeometryReverse)) {
                        var featureRef2 = new FeatureRef {
                            Id = hashGeometry,
                            Reverse = true,
                        };
                        featureRefs.Add(hashGeometryReverse, featureRef2);
                    }
                    if (!featureRefs2Reverse.ContainsKey(hashGeometry))
                        featureRefs2Reverse.Add(hashGeometry, hashGeometryReverse);
                    if (!featureRefs2Reverse.ContainsKey(hashGeometryReverse))
                        featureRefs2Reverse.Add(hashGeometryReverse, hashGeometry);
                }
            }

            /*
                For each LINESTRING, resolve against the existing network
                var lineEdges = network.ResolveLineString(myLineString, allEdges);
                lineEdges is an ordered List<MergedEdge> covering the full linestring path:
                - existing canonical edges where the linestring follows the network
                - ephemeral single-segment edges (ConstituentEdges.Count == 0) for portions
                  that don't coincide with any network edge
             */

            // TEST CreateLinearRing
            if (System.Diagnostics.Debugger.IsAttached) {
                int[] error_Multipart = [];
                int[] error_LinearRing = [];
                foreach (var sourceId in this._mixedTopologyNetwork.Sources) {
                    var edges = sourceRefs[sourceId];
                    if (!edges.Any()) {
                        continue;
                    }

                    if (edges.Count > 1 && edges[0].Geometry.Equals(edges[^1].Geometry))
                        edges = edges[..^1];


                    //if (sourceId == 2653) {
                    //    var ee = edges.Select(e => e.Geometry.ToText()).ToArray();
                    //    System.Diagnostics.Debugger.Break();
                    //}

                    if (edges.Count > 1) {
                        if (this._sourceLineType[sourceId] != LineType.Curve) {
                            var merged = this._mixedTopologyNetwork.AssembleLinearRing(edges);
                        }
                        //        var merged = MergeOrderedLineStrings(edges.Select(e => e.Geometry));

                        //        if (this._sourceLineType[sourceId] != LineType.Curve) {
                        //            try {
                        //                var linearRing = Reloaded.Factory!.CreateLinearRing(merged.Coordinates);
                        //            }
                        //            catch (System.Exception ex) {
                        //                error_LinearRing = [.. error_LinearRing, sourceId];
                        //                this._interceptor?.Invoke(100, [.. edges.Select(e => (e.Geometry, $"{sourceId} LinearRing: {e.Geometry.ToText()}"))]);
                        //                continue;                                
                        //            }
                        //        }
                    }

                }

                if (error_Multipart.Any() || error_LinearRing.Any()) {
                    System.Diagnostics.Debugger.Break();
                }
            }

            (MergedEdge[] edges, ulong hashGeometry)[] dictionaryEdges = [];

            (ulong id, HashSet<ulong> hashset, List<MergedEdge> edges)[] dictionaryCompositeCurves = [];

            int[] empty_sources = [];
            foreach (var sourceId in this._mixedTopologyNetwork.Sources) {
                //if (sourceId == 27) System.Diagnostics.Debugger.Break();                
                if (checks.Contains(sourceId)) System.Diagnostics.Debugger.Break();

                var edges = sourceRefs[sourceId];
                if (!edges.Any()) {
                    empty_sources = [.. empty_sources, sourceId];
                    continue;
                }
                if (edges.Count > 1 && edges[0].Geometry.Equals(edges[^1].Geometry))
                    edges = edges[..^1];

                if (edges.Count > 1) {
                    //bool reverse = false;

                    //if (this._sourceLineType[sourceId] != LineType.Curve) {
                    //    var linearRing = this._mixedTopologyNetwork.AssembleLinearRing(edges);
                    //    var isCCW = linearRing.IsCCW;

                    //    if (this._sourceLineType[sourceId] == LineType.Exterior) {
                    //        if (isCCW) {
                    //            reverse = true;
                    //        }
                    //    }
                    //    else if (!isCCW) {
                    //        reverse = true;
                    //    }
                    //}
                    //else {
                    //    var merged = this._mixedTopologyNetwork.AssembleLineString(edges);
                    //    var slope = merged.Slope();
                    //    if (Math.Sign(this._sourceSlope[sourceId]) != Math.Sign(slope)) {
                    //        reverse = true;
                    //    }
                    //}

                    var sortedlist = new SortedList<int, FeatureRef>();

                    List<EdgeReference> assembleEdges = [];

                    if (this._sourceLineType[sourceId] == LineType.Curve) {
                        assembleEdges = this._mixedTopologyNetwork.AssembleEdgeOrderForLineString(edges);
                    }
                    else {
                        assembleEdges = this._mixedTopologyNetwork.AssembleEdgeOrderForLinearRing(edges);

                        if (checks.Contains(sourceId)) {
                            var _edges = edges.Select(e => e.Geometry).ToArray();
                        }
                    }

                    int count = 0;
                    foreach (var e in assembleEdges) {
                        ulong hashGeometry = System.IO.Hashing.XxHash32.HashToUInt32(e.OrientedGeometry.AsBinary());

                        sortedlist.Add(count++, featureRefs[hashGeometry]);
                    }
                    featureRefUsed = [.. featureRefUsed, .. sortedlist.Values.Select(e => e.Id)];

                    if (checks.Contains(sourceId)) {
                        var compositeLineString = string.Join(',', sortedlist.Select(e => e.Value.Reverse ? $"RC{e.Value.Id}" : $"C{e.Value.Id}"));

                        //this._interceptor?.Invoke(100, [.. sortedlist.Select(e => (e.Value.Reverse ? this._curves[e.Value.Id].LineStringReverse : this._curves[e.Value.Id].LineString, $"{e.Value.Reverse} {this._curves[e.Value.Id].LineStringText}"))]);

                        this._interceptor?.Invoke(100, [.. assembleEdges.Select(e => (e.OrientedGeometry, $"{sourceId} {e.OrientedGeometry}"))]);

                        var fullChain = this._mixedTopologyNetwork.GetFullEdgeChainFor(sourceId);

                        System.Diagnostics.Debugger.Break();
                    }

                    ulong compositeCurveId = ulong.MinValue;

                    var hasset = sortedlist.Values.Select(e => e.Id).ToHashSet();
                    if (dictionaryCompositeCurves.Any(e => e.hashset.SetEquals(hasset))) {
                        var _compositeCurve = dictionaryCompositeCurves.Single(e => e.hashset.SetEquals(hasset));
                        compositeCurveId = _compositeCurve.id;
                    }
                    else {
                        var _compositeCurve = new CompositeCurveFeature([.. sortedlist.Values]);
                        dictionaryCompositeCurves = [.. dictionaryCompositeCurves, (_compositeCurve.Id, hasset, edges)];

                        this._compositecurves.Add(_compositeCurve.Id, _compositeCurve);

                        featureRefs.Add(_compositeCurve.Id, new FeatureRef {
                            Id = _compositeCurve.Id,
                            Reverse = false,
                        });
                        featureRefs.Add(_compositeCurve.Reverse, new FeatureRef {
                            Id = _compositeCurve.Id,
                            Reverse = true,
                        });
                        featureRefs2Reverse.Add(_compositeCurve.Id, _compositeCurve.Reverse);
                        featureRefs2Reverse.Add(_compositeCurve.Reverse, _compositeCurve.Id);

                        compositeCurveId = _compositeCurve.Id;
                    }


                    if (this._sourceLineType[sourceId] != LineType.Curve) {
                        var linearRing = this._mixedTopologyNetwork.AssembleLinearRing(dictionaryCompositeCurves.Single(e=>e.id==compositeCurveId).edges);
                        var isCCW = linearRing.IsCCW;

                        if (this._sourceLineType[sourceId] == LineType.Exterior) {
                            if (isCCW) {
                                compositeCurveId = featureRefs2Reverse[compositeCurveId];
                            }
                        }
                        else if (!isCCW) {
                            compositeCurveId = featureRefs2Reverse[compositeCurveId];
                        }
                    }
                    else {
                        var merged = this._mixedTopologyNetwork.AssembleLineString(dictionaryCompositeCurves.Single(e => e.id == compositeCurveId).edges);
                        var slope = merged.Slope();
                        if (Math.Sign(this._sourceSlope[sourceId]) != Math.Sign(slope)) {
                            compositeCurveId = featureRefs2Reverse[compositeCurveId];
                        }
                    }

                    sourceId2FeatureRef.Add(sourceId, compositeCurveId);
                    if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                        var id = $"C{compositeCurveId}";
                        this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                    }
                }
                else {
                    ulong hashGeometry = System.IO.Hashing.XxHash32.HashToUInt32(edges[0].Geometry.AsBinary());
                    hashGeometry = featureRefs[hashGeometry].Id;

                    foreach (var e in dictionaryEdges) {
                        if (e.edges.Length > 1) continue;
                        if (e.edges[0].Geometry.Equals(edges[0].Geometry)) {
                            hashGeometry = e.hashGeometry;
                            break;
                        }
                    }
                    dictionaryEdges = [.. dictionaryEdges, ([.. edges], hashGeometry)];


                    if (this._sourceLineType[sourceId] != LineType.Curve) {
                        if (checks.Contains(sourceId)) {
                            var txt = edges[0].Geometry.ToText();
                            this._interceptor?.Invoke(100, [(edges[0].Geometry, $"{edges[0].Geometry.ToText()}")]);
                            System.Diagnostics.Debugger.Break();
                        }

                        var linearRing = Reloaded.Factory!.CreateLinearRing(this._curves[hashGeometry].LineString.Coordinates);
                        var isCCW = linearRing.IsCCW;

                        if (this._sourceLineType[sourceId] == LineType.Exterior) {
                            if (isCCW) {
                                hashGeometry = featureRefs2Reverse[hashGeometry];
                            }
                        }
                        else if (!isCCW) {
                            hashGeometry = featureRefs2Reverse[hashGeometry];
                        }
                    }
                    else {
                        var slope = this._curves[hashGeometry].LineString.Slope();
                        if (Math.Sign(this._sourceSlope[sourceId]) != Math.Sign(slope)) {
                            hashGeometry = featureRefs2Reverse[hashGeometry];
                        }
                    }
                    featureRefUsed = [.. featureRefUsed, hashGeometry];

                    var id = featureRefs[hashGeometry].Reverse ? $"RC{featureRefs[hashGeometry].Id}" : $"C{featureRefs[hashGeometry].Id}";

                    sourceId2FeatureRef.Add(sourceId, hashGeometry);
                    if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                        this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                    }
                }
            }

            // Get real hash values for used curves
            var _ = featureRefs.Where(e => featureRefUsed.Contains(e.Key)).Select(e => e.Value.Id);
            this._curves = this._curves.Where(e => _.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value);

            foreach (var polygon in this._featureMapperPolygons) {
                var uid = polygon.Key;

                FeatureRef exteriorRing = featureRefs[sourceId2FeatureRef[polygon.Value.ExteriorRing]];
                FeatureRef[] interior = [];
                if (polygon.Value.InteriorRing != default) {
                    for (int i = 0; i < polygon.Value.InteriorRing.Length; i++) {
                        var featureRef = featureRefs[sourceId2FeatureRef[polygon.Value.InteriorRing[i]]];
                        interior = [.. interior, featureRef];
                    }
                }

                var surface = new SurfaceFeature {
                    Id = ulong.Parse(uid.Substring(1)),
                    Exterior = exteriorRing,
                    Interior = interior,
                    Ref = uid,
                };

                if (!this._surfaces.ContainsKey(surface.Id))
                    this._surfaces.Add(surface.Id, surface);

                this._mapping.Add(uid, $"S{surface.Id}");
            }

            //this._interceptor?.Invoke(6000, [.. this._curves.Select(e => (e.Value.LineString, $"{e.Value.Id}"))]);
            this._interceptor?.Invoke(100, [.. this._curves.Select(e => (e.Value.LineString, $"{e.Value.Id}"))]);

            return this;
        }

        private IDictionary<ulong, CurveFeature> _curves = new Dictionary<ulong, CurveFeature>();
        private readonly IDictionary<ulong, CompositeCurveFeature> _compositecurves = new Dictionary<ulong, CompositeCurveFeature>();
        private readonly IDictionary<ulong, SurfaceFeature> _surfaces = new Dictionary<ulong, SurfaceFeature>();

        IEnumerable<CurveFeature> IMatrix.Curves => this._curves.Values;

        IEnumerable<CompositeCurveFeature> IMatrix.CompositeCurves => this._compositecurves.Values;

        IEnumerable<SurfaceFeature> IMatrix.Surfaces => this._surfaces.Values;

        IDictionary<string, string> IMatrix.MappingFOID => this._mapping;

        public record PolygonSource(int ExteriorRing, int[] InteriorRing);

        string[] checks_linestrings = [];


        enum LineType : int
        {
            Exterior = 1,
            Interior = 2,
            Curve = 4,
        };

        private readonly Dictionary<int, LineType> _sourceLineType = [];
        private readonly Dictionary<int, double> _sourceSlope = [];

        private int[] checks = [];

        private Geometry[] _geometries = [];

        private ITopologyBuilder AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves, bool isTopology) {
            foreach (var surface in surfaces) {
                if (System.Diagnostics.Debugger.IsAttached)
                    _geometries = [.. _geometries, surface.ExteriorRing];

                var idExteriorRing = this._mixedTopologyNetwork.AddLineString(surface.ExteriorRing);

                //if (surface.UID.EndsWith("10800027198")) {
                //    //checks = [.. checks, idExteriorRing];
                //}
                //if (surface.UID.EndsWith("10400030449")) {
                //    checks = [.. checks, idExteriorRing];
                //}

                if (surface.UID.EndsWith("10800061878")) {
                    checks = [.. checks, idExteriorRing];
                }
                //if (surface.UID.EndsWith("10800027198")) {
                //    checks = [.. checks, idExteriorRing];
                //}



                this._sourceLineType.Add(idExteriorRing, LineType.Exterior);

                var idInteriorRings = new int[0];
                foreach (var interior in surface.InteriorRings) {
                    if (System.Diagnostics.Debugger.IsAttached)
                        _geometries = [.. _geometries, interior];

                    var id = this._mixedTopologyNetwork.AddLineString(interior);
                    idInteriorRings = [.. idInteriorRings, id];

                    this._sourceLineType.Add(id, LineType.Interior);

                    if (checks.Contains(id)) {
                        this.checks_linestrings = [.. this.checks_linestrings, interior.ToText()];
                    }
                }

                if (checks.Contains(idExteriorRing)) {
                    this.checks_linestrings = [.. this.checks_linestrings, surface.ExteriorRing.ToText()];
                }

                var p = new PolygonSource(idExteriorRing, idInteriorRings);
                this._featureMapperPolygons.Add(surface.Name, p);
            }

            foreach (var curve in curves) {
                //if (curve.UID.EndsWith("10100081766")) System.Diagnostics.Debugger.Break();
                if (System.Diagnostics.Debugger.IsAttached)
                    _geometries = [.. _geometries, curve.LineString];

                var id = this._mixedTopologyNetwork.AddLineString(curve.LineString);
                if (id < 0) continue;
                this._sourceLineType.Add(id, LineType.Curve);

                this._sourceSlope.Add(id, curve.LineString.Slope());

                if (checks.Contains(id)) {
                    this.checks_linestrings = [.. this.checks_linestrings, curve.LineString.ToText()];
                }

                this._featureMapperLineStrings.Add(curve.Name, id);
            }

            return this;
        }

        private static bool ContainsSegment(string lineString, string segment) {
            if (lineString.Equals(segment)) return true;

            if (lineString.Contains(segment + ",")) return true;
            if (lineString.Contains(", " + segment)) return true;

            //  MultiLineString
            if (lineString.Contains(segment + ")")) return true;
            if (lineString.Contains(segment + "),")) return true;
            if (lineString.Contains("), " + segment)) return true;

            return false;
        }

        private static int IndexOfSegment(string lineString, string segment) {
            if (lineString.Equals(segment)) return 0;

            if (lineString.Contains(segment + ",")) return lineString.IndexOf(segment + ",");
            if (lineString.Contains(", " + segment)) return lineString.IndexOf(", " + segment);

            //  MultiLineString
            if (lineString.Contains(segment + ")")) return lineString.IndexOf(segment + ")");
            if (lineString.Contains(segment + "),")) return lineString.IndexOf(segment + "),");
            if (lineString.Contains("), " + segment)) return lineString.IndexOf("), " + segment);

            throw new IndexOutOfRangeException();
        }

        //private LineString LineStringBuilder(IList<MergedEdge> edges) {
        //    var lineMerger = new LineMerger();
        //    foreach (var edge in edges) {
        //        lineMerger.Add(edge.Geometry);
        //    }

        //    var mergedLineStrings = lineMerger.GetMergedLineStrings();
        //    if (mergedLineStrings.Count > 1)
        //        throw new InvalidOperationException("Merged LineString can't be a multipart geometry!");
        //    var merged = (LineString)mergedLineStrings[0];
        //    return merged;

        //    //Coordinate[] coords = [.. edges[0].Edge.Geometry.Coordinates];
        //    //for (int i = 1; i < edges.Count; i++) {
        //    //    var edge = edges.Single(e => e.Edge.Geometry.Coordinates[0].Equals2D(coords[^1]));
        //    //    var index = edges.IndexOf(edge);

        //    //    if (edge.Edge.Geometry.Coordinates[0].Equals2D(coords[^1]))
        //    //        coords = [.. coords, .. edge.Edge.Geometry.Coordinates];
        //    //    else {
        //    //        if (!coords[^1].Equals2D(edge.Edge.Geometry.Coordinates[^1])) System.Diagnostics.Debugger.Break();
        //    //        coords = [.. coords, .. edge.Edge.Geometry.Coordinates.Reverse()];
        //    //    }
        //    //}
        //    //return Reloaded.Factory!.CreateLineString(coords);
        //}

        public LineString MergeOrderedLineStrings(IEnumerable<LineString> lines) {
            var segments = lines.ToList();

            // Build adjacency: map from endpoint coordinate to (segmentIndex, isReversed)
            // We'll find the correct traversal order via graph walk

            bool CoordEquals(Coordinate a, Coordinate b) => a.Equals2D(b);
            //a.Distance(b) <= Reloaded.Factory!.PrecisionModel.GridSize;

            // Find a start segment: one whose start point is not 
            // the end point of any other segment (i.e. a dangling start)
            // Collect all start and end points
            var allStarts = segments.Select(s => s.StartPoint.Coordinate).ToList();
            var allEnds = segments.Select(s => s.EndPoint.Coordinate).ToList();

            // A true start is an endpoint that appears as a start (or end if reversed)
            // but not as the end of another segment — try each candidate
            int FindStartSegment(out bool reversed) {
                for (int i = 0; i < segments.Count; i++) {
                    var start = segments[i].StartPoint.Coordinate;
                    var end = segments[i].EndPoint.Coordinate;

                    bool startIsConnectedTo = allEnds
                        .Where((_, j) => j != i)
                        .Any(e => CoordEquals(e, start));
                    bool startIsConnectedFrom = allStarts
                        .Where((_, j) => j != i)
                        .Any(s => CoordEquals(s, start));

                    bool endIsConnectedTo = allEnds
                        .Where((_, j) => j != i)
                        .Any(e => CoordEquals(e, end));
                    bool endIsConnectedFrom = allStarts
                        .Where((_, j) => j != i)
                        .Any(s => CoordEquals(s, end));

                    // A dangling start: this end is not the destination of any other segment
                    if (!startIsConnectedTo && !startIsConnectedFrom) {
                        reversed = false;
                        return i;
                    }
                    if (!endIsConnectedTo && !endIsConnectedFrom) {
                        reversed = true;
                        return i;
                    }
                }
                // Closed ring — just pick first segment unreversed
                reversed = false;
                return 0;
            }

            var used = new bool[segments.Count];
            var orderedCoords = new List<Coordinate>();

            bool startReversed;
            int startIdx = FindStartSegment(out startReversed);

            var firstSeg = segments[startIdx];
            var firstCoords = startReversed
                ? firstSeg.Coordinates.Reverse().ToArray()
                : firstSeg.Coordinates;
            orderedCoords.AddRange(firstCoords);
            used[startIdx] = true;

            // Walk: repeatedly find the next unused segment that connects to current tail
            for (int step = 1; step < segments.Count; step++) {
                var tail = orderedCoords[^1];
                bool found = false;

                for (int i = 0; i < segments.Count; i++) {
                    if (used[i]) continue;

                    var segStart = segments[i].StartPoint.Coordinate;
                    var segEnd = segments[i].EndPoint.Coordinate;

                    if (CoordEquals(tail, segStart)) {
                        orderedCoords.AddRange(segments[i].Coordinates.Skip(1));
                        used[i] = true;
                        found = true;
                        break;
                    }
                    if (CoordEquals(tail, segEnd)) {
                        orderedCoords.AddRange(segments[i].Coordinates.Reverse().Skip(1));
                        used[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    throw new InvalidOperationException(
                        $"No connecting segment found at step {step}. " +
                        $"Tail coordinate: {orderedCoords[^1]}");
            }

            return Reloaded.Factory!.CreateLineString(orderedCoords.ToArray());
        }
    }
}