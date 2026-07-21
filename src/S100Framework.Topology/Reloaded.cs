using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestTopology")]

namespace S100FC.Topology
{
    using GeoAPI.Geometries;
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

            public int GetHashCode(LineString e) => (int)System.IO.Hashing.XxHash64.HashToUInt64(e.AsBinary());
        }

        private class Surface
        {
            public int Id { get; init; }

            public required string Exterior { get; init; }

            public string[]? Interior { get; set; } = default;

            public string Name => $"S{this.Id}";
        }

        public static GeometryFactory? Factory { get; set; } = default; // new GeometryFactory(new PrecisionModel(10000000), srid: 4326); // Or PrecisionModels.Floating

        private Action<int, ICollection<(LineString lineString, string message)>, bool>? _interceptor;
        private ILogger<Reloaded>? _logger;

        private readonly MixedTopologyNetwork _mixedTopologyNetwork;

        private readonly TopologyBuilder _topologyBuilder;

        private readonly Dictionary<string, PolygonSource> _featureMapperPolygons = [];
        private readonly Dictionary<string, int> _featureMapperLineStrings = [];

        private readonly Dictionary<string, string> _mapping = [];

        private S100FC.Topology.Polygon[] _collapse = [];

        protected Reloaded() {
            //  Protected default constructor            
            this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory!, snapTolerance: Reloaded.Factory!.PrecisionModel.GridSize);

            this._topologyBuilder = new TopologyBuilder();
        }

        public static ITopologyBuilder CreateMatrix(Action<int, ICollection<(LineString lineString, string message)>, bool>? interceptor = default, ILogger<Reloaded>? logger = default) {
            return new Reloaded() {
                _interceptor = interceptor,
                _logger = logger,
            };
        }

        GeometryFactory IMatrixReloaded.Factory => Reloaded.Factory!;

        ITopologyBuilder ITopologyBuilder.AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddFeatures(surfaces, curves);

        //ITopologyBuilder ITopologyBuilder.AddGroup2Features(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddFeatures(surfaces, curves, true);

        ITopologyBuilder ITopologyBuilder.AddSingletonFeatures(IList<S100FC.Topology.Polyline> curves) => this.AddSingletonFeatures(curves);

        public GeometryPrecisionReducer Reducer => new GeometryPrecisionReducer(Factory!.PrecisionModel) {
            Pointwise = false,
            RemoveCollapsedComponents = true,
            ChangePrecisionModel = true,
        };

        IMatrix ITopologyBuilder.BuildTopology() {
            this._mapping.Clear();
            if (_logger is not null)
                this._mixedTopologyNetwork.Logger = _logger;
            this._mixedTopologyNetwork.Build();

            //if (this.checks.Any()) System.Diagnostics.Debugger.Break();
            //foreach (var sourceId in this.checks) {
            //    var edges = this._mixedTopologyNetwork.GetEdgesFor(sourceId);
            //    var wkt = edges.Select(e => e.Geometry.ToText()).ToArray();

            //    //this._interceptor?.Invoke(100, [.. edges.Select(e => (e.Geometry, $"{e.Geometry.ToText()}"))]);
            //}

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

                            this._interceptor?.Invoke(100, [(existing, $"{existing.ToText()}"), (overlapping, $"{overlapping.ToText()}")], true);
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
                                    this._interceptor?.Invoke(100, [.. edges.Select(e => (e.Geometry, $"{e.Geometry.ToText()}"))], true);
                                }

                                this._interceptor?.Invoke(100, [(existing, $"{existing.ToText()}"), (overlapping, $"{overlapping.ToText()}")], true);
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
                    var hashGeometry = System.IO.Hashing.XxHash64.HashToUInt64(e.Geometry.AsBinary());
                    if (!featureRefs.ContainsKey(hashGeometry)) {
                        var featureRef1 = new FeatureRef {
                            Id = hashGeometry,
                            Reverse = false,
                        };
                        featureRefs.Add(hashGeometry, featureRef1);

                        var curve = new CurveFeature(e.Geometry, hashGeometry);
                        this._curves.Add(featureRef1.Id, curve);
                    }

                    var hashGeometryReverse = System.IO.Hashing.XxHash64.HashToUInt64(e.Geometry.Reverse().AsBinary());
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

            (MergedEdge[] edges, ulong hashGeometry)[] dictionaryEdges = [];

            (ulong id, HashSet<ulong> hashset, List<MergedEdge> edges)[] dictionaryCompositeCurves = [];

            int[] empty_sources = [];


            void BuildSource(int sourceId, List<MergedEdge> edges) {
                if (edges.Count > 1 && edges[0].Geometry.Equals(edges[^1].Geometry))
                    edges = edges[..^1];

                if (edges.Count > 1) {
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
                        ulong hashGeometry = System.IO.Hashing.XxHash64.HashToUInt64(e.OrientedGeometry.AsBinary());

                        sortedlist.Add(count++, featureRefs[hashGeometry]);
                    }
                    featureRefUsed = [.. featureRefUsed, .. sortedlist.Values.Select(e => e.Id)];

                    if (checks.Contains(sourceId)) {
                        var compositeLineString = string.Join(',', sortedlist.Select(e => e.Value.Reverse ? $"RC{e.Value.Id}" : $"C{e.Value.Id}"));

                        //this._interceptor?.Invoke(100, [.. sortedlist.Select(e => (e.Value.Reverse ? this._curves[e.Value.Id].LineStringReverse : this._curves[e.Value.Id].LineString, $"{e.Value.Reverse} {this._curves[e.Value.Id].LineStringText}"))]);

                        if (assembleEdges.Any())
                            this._interceptor?.Invoke(100, [.. assembleEdges.Select(e => (e.OrientedGeometry, $"{sourceId} {e.OrientedGeometry}"))], true);
                        else
                            this._interceptor?.Invoke(100, [.. edges.Select(e => (e.Geometry, $"{sourceId} {e.Geometry}"))], true);

                        var fullChain = this._mixedTopologyNetwork.GetFullEdgeChainFor(sourceId);

                        //this._interceptor?.Invoke(100, [.. fullChain.Select(e => (e.Geometry, $"{sourceId} {e.Geometry}"))]);

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

                        //if (_compositeCurve.Id == 4112964597) System.Diagnostics.Debugger.Break();
                        //if (_compositeCurve.Reverse == 3210164099) System.Diagnostics.Debugger.Break();                        

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
                        var linearRing = this._mixedTopologyNetwork.AssembleLinearRing(dictionaryCompositeCurves.Single(e => e.id == compositeCurveId).edges);
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

                    featureRefUsed = [.. featureRefUsed, featureRefs[compositeCurveId].Id];

                    sourceId2FeatureRef.Add(sourceId, compositeCurveId);
                    if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                        var id = featureRefs[compositeCurveId].Reverse ? $"RC{featureRefs[compositeCurveId].Id}" : $"C{featureRefs[compositeCurveId].Id}";
                        this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                    }
                }
                else {
                    ulong hashGeometry = System.IO.Hashing.XxHash64.HashToUInt64(edges[0].Geometry.AsBinary());
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
                            this._interceptor?.Invoke(100, [(edges[0].Geometry, $"{edges[0].Geometry.ToText()}")], true);
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
                    //featureRefUsed = [.. featureRefUsed, hashGeometry];                    
                    featureRefUsed = [.. featureRefUsed, featureRefs[hashGeometry].Id];

                    sourceId2FeatureRef.Add(sourceId, hashGeometry);
                    if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                        var id = featureRefs[hashGeometry].Reverse ? $"RC{featureRefs[hashGeometry].Id}" : $"C{featureRefs[hashGeometry].Id}";
                        this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                    }
                }
            }

            foreach (var sourceId in this._mixedTopologyNetwork.Sources) {
                //if (sourceId == 27) System.Diagnostics.Debugger.Break();                
                if (checks.Contains(sourceId)) System.Diagnostics.Debugger.Break();

                var edges = sourceRefs[sourceId];
                if (!edges.Any()) {
                    empty_sources = [.. empty_sources, sourceId];
                    continue;
                }
                BuildSource(sourceId, edges);
            }

            ;
            //foreach(var geometry in this._group2Geometries) {
            //    var edges = this._mixedTopologyNetwork.ResolveLineString((LineString)geometry.Geometry, allEdges);
            //    if (!edges.Any()) {
            //        empty_sources = [.. empty_sources, geometry.Id];
            //        continue;
            //    }

            //    foreach (var e in edges) {
            //        var hashGeometry = System.IO.Hashing.XxHash64.HashToUInt64(e.Geometry.AsBinary());
            //        if (!featureRefs.ContainsKey(hashGeometry)) {
            //            var featureRef1 = new FeatureRef {
            //                Id = hashGeometry,
            //                Reverse = false,
            //            };
            //            featureRefs.Add(hashGeometry, featureRef1);

            //            var curve = new CurveFeature(e.Geometry, hashGeometry);
            //            this._curves.Add(featureRef1.Id, curve);
            //        }

            //        var hashGeometryReverse = System.IO.Hashing.XxHash64.HashToUInt64(e.Geometry.Reverse().AsBinary());
            //        if (!featureRefs.ContainsKey(hashGeometryReverse)) {
            //            var featureRef2 = new FeatureRef {
            //                Id = hashGeometry,
            //                Reverse = true,
            //            };
            //            featureRefs.Add(hashGeometryReverse, featureRef2);
            //        }
            //        if (!featureRefs2Reverse.ContainsKey(hashGeometry))
            //            featureRefs2Reverse.Add(hashGeometry, hashGeometryReverse);
            //        if (!featureRefs2Reverse.ContainsKey(hashGeometryReverse))
            //            featureRefs2Reverse.Add(hashGeometryReverse, hashGeometry);
            //    }

            //    BuildSource(geometry.Id, edges);
            //}

            ;
            // Get real hash values for used curves
            //var _ = featureRefs.Where(e => featureRefUsed.Contains(e.Key)).Select(e => e.Value.Id);
            var idSet = featureRefUsed.ToHashSet(); ;
            this._curves = this._curves.Where(e => idSet.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value);

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

            //this._interceptor?.Invoke(6000, [.. this._curves.Select(e => (e.Value.LineString, $"{e.Value.Id}"))],false);
            this._interceptor?.Invoke(100, [.. this._curves.Select(e => (e.Value.LineString, $"#{e.Value.Id} {e.Value.LineStringText}"))], false);

            return this;
        }

        private IDictionary<ulong, CurveFeature> _curves = new Dictionary<ulong, CurveFeature>();
        private readonly IDictionary<ulong, CompositeCurveFeature> _compositecurves = new Dictionary<ulong, CompositeCurveFeature>();
        private readonly IDictionary<ulong, SurfaceFeature> _surfaces = new Dictionary<ulong, SurfaceFeature>();

        IEnumerable<CurveFeature> IMatrix.Curves => this._curves.Values;

        IEnumerable<CompositeCurveFeature> IMatrix.CompositeCurves => this._compositecurves.Values;

        IEnumerable<SurfaceFeature> IMatrix.Surfaces => this._surfaces.Values;

        IDictionary<string, string> IMatrix.MappingFOID => this._mapping;

        public ICollection<string> Collapse => [.. this._collapse.Select(e => e.UID)];

        public record PolygonSource(int ExteriorRing, int[] InteriorRing);

        string[] checks_linestrings = [];


        enum LineType : int
        {
            Exterior = 1,
            Interior = 2,
            Curve = 4,
            Ring = 8,
        };

        private readonly Dictionary<int, LineType> _sourceLineType = [];
        private readonly Dictionary<int, double> _sourceSlope = [];

        private int[] checks = [];

        private Geometry[] _geometriesTopology = [];

        private Geometry[] _geometries = [];

        public string[] NetworkTopology => [.. _geometriesTopology.Select(e => e.ToText())];

        public string[] Geometries => [.. _geometries.Select(e => e.ToText())];

        private int _id = int.MaxValue;

        private List<NetworkGeometry> _group2Geometries = [];

        private ITopologyBuilder AddFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) {
            foreach (var surface in surfaces) {
                var checkExteriorRing = this._mixedTopologyNetwork.CheckRingCollapse((LinearRing)surface.ExteriorRing);
                if (checkExteriorRing.WillCollapse) {
                    //System.Diagnostics.Debugger.Break();
                    _collapse = [.. _collapse, surface];
                    continue;
                }
                if (System.Diagnostics.Debugger.IsAttached)
                    _geometriesTopology = [.. _geometriesTopology, surface.ExteriorRing];


                var idExteriorRing = this._mixedTopologyNetwork.AddLineString(surface.ExteriorRing);

                //if (surface.UID.EndsWith("10400000009")) {
                //    checks = [.. checks, idExteriorRing];
                //}
                //if (surface.UID.EndsWith("10400000007")) {
                //    checks = [.. checks, idExteriorRing];
                //    //this._interceptor?.Invoke(6000, [(surface.ExteriorRing, "F10400001741")]);
                //}


                this._sourceLineType.Add(idExteriorRing, LineType.Exterior);

                if (checks.Contains(idExteriorRing)) {
                    this.checks_linestrings = [.. this.checks_linestrings, surface.ExteriorRing.ToText()];

                    var reverse = surface.ExteriorRing.Reverse().ToText();
                }

                var idInteriorRings = new int[0];
                foreach (var interior in surface.InteriorRings) {
                    var checkInteriorRing = this._mixedTopologyNetwork.CheckRingCollapse((LinearRing)interior);
                    if (checkInteriorRing.WillCollapse) {
                        continue;
                    }
                    if (System.Diagnostics.Debugger.IsAttached)
                        _geometriesTopology = [.. _geometriesTopology, interior];

                    var id = this._mixedTopologyNetwork.AddLineString(interior);
                    idInteriorRings = [.. idInteriorRings, id];

                    this._sourceLineType.Add(id, LineType.Interior);

                    if (checks.Contains(id)) {
                        this.checks_linestrings = [.. this.checks_linestrings, interior.ToText()];

                        var reverse = interior.Reverse().ToText();
                    }
                }

                var p = new PolygonSource(idExteriorRing, idInteriorRings);
                this._featureMapperPolygons.Add(surface.Name, p);
            }

            foreach (var curve in curves) {
                //if (curve.UID.EndsWith("10100081766")) System.Diagnostics.Debugger.Break();
                var id = this._mixedTopologyNetwork.AddLineString(curve.LineString);
                if (id < 0) continue;
                if (System.Diagnostics.Debugger.IsAttached)
                    _geometriesTopology = [.. _geometriesTopology, curve.LineString];

                if (curve.LineString is LinearRing linearring)
                    this._sourceLineType.Add(id, LineType.Ring);
                else
                    this._sourceLineType.Add(id, LineType.Curve);

                this._sourceSlope.Add(id, curve.LineString.Slope());

                //if (curve.UID.EndsWith("10100001235")) {
                //    checks = [.. checks, id];
                //}

                if (checks.Contains(id)) {
                    this.checks_linestrings = [.. this.checks_linestrings, curve.LineString.ToText()];
                }

                this._featureMapperLineStrings.Add(curve.Name, id);
            }
            return this;
        }

        private ITopologyBuilder AddSingletonFeatures(IList<Polyline> curves) {
            return this;
        }
    }
}