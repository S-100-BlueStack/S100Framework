using NetTopologySuite.Geometries;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestTopology")]

namespace S100FC.Topology
{
    using NetTopologySuite.Operation.Linemerge;
    using NetTopologySuite.Precision;
    using S100Framework.Topology.Internal;
    using System.Collections;
    using System.Net;
    using static S100Framework.Topology.Internal.MixedTopologyNetwork;

    public interface IMatrixReloaded : IMatrix
    {
        GeometryFactory Factory { get; }
    }

    public class Reloaded : ITopologyBuilder, IMatrixReloaded
    {
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

        private readonly Dictionary<string, PolygonSource> _featureMapperPolygons = [];
        private readonly Dictionary<string, int> _featureMapperLineStrings = [];

        private readonly Dictionary<string, string> _mapping = [];

        protected Reloaded() {
            //  Protected default constructor            


            //this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory!, snapTolerance: 0.000000001);
            //this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory, snapTolerance: 0.000000005);
            //this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory!, snapTolerance: 0.0000001);                                                                                                    
            this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory!, snapTolerance: Reloaded.Factory!.PrecisionModel.GridSize);
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

            //this._interceptor?.Invoke(100, [.. this._mixedTopologyNetwork.Edges.Select(e => (e.Geometry, $"{e.Id}"))]);

            var featureRefs = new Dictionary<ulong, FeatureRef>();
            var featureRefs2Reverse = new Dictionary<ulong, ulong>();

            var sourceId2FeatureRef = new Dictionary<int, ulong>();

            var (allEdges, sourceRefs) = this._mixedTopologyNetwork.BuildEdgeIndex();

            foreach (var e in allEdges) {
                var hashGeometry = System.IO.Hashing.XxHash32.HashToUInt32(e.Geometry.AsBinary());
                if (!featureRefs.ContainsKey(hashGeometry)) {
                    var featureRef1 = new FeatureRef {
                        Id = hashGeometry,
                        Reverse = false,
                    };
                    featureRefs.Add(hashGeometry, featureRef1);

                    var curve = new CurveFeature(e.Geometry, hashGeometry);
                    this._curves.Add(featureRef1.Id, curve);
                }
                var hashGeometryReverse = System.IO.Hashing.XxHash32.HashToUInt32(e.Geometry.Reverse().AsBinary());
                if (!featureRefs.ContainsKey(hashGeometryReverse)) {
                    var featureRef2 = new FeatureRef {
                        Id = hashGeometry,
                        Reverse = true,
                    };
                    featureRefs.Add(hashGeometryReverse, featureRef2);
                }
                featureRefs2Reverse.Add(hashGeometry, hashGeometryReverse);
                featureRefs2Reverse.Add(hashGeometryReverse, hashGeometryReverse);
            }

            int[] empty_sources = [];
            foreach (var sourceId in this._mixedTopologyNetwork.Sources) {
                //if (sourceId == 1181) System.Diagnostics.Debugger.Break();                

                var edges = sourceRefs[sourceId];
                if (!edges.Any()) {
                    empty_sources = [.. empty_sources, sourceId];
                    continue;
                }

                if (edges.Count > 1) {
                    FeatureRef[] refs = [];

                    var lineMerger = new LineMerger();
                    foreach (var edge in edges) {
                        lineMerger.Add(edge.Edge.Geometry);

                        //if (sourceId == 1174) {
                        //    if (edge.Forward)
                        //        this._interceptor?.Invoke(100, [(edge.Edge.Geometry, $"Forward: {edge.Forward}")]);
                        //    else
                        //        this._interceptor?.Invoke(100, [((LineString)edge.Edge.Geometry.Reverse(), $"Forward: {edge.Forward}")]);
                        //}
                    }

                    var mergedLineStrings = lineMerger.GetMergedLineStrings();
                    if (mergedLineStrings.Count > 1) System.Diagnostics.Debugger.Break();
                    var merged = (LineString)mergedLineStrings[0];

                    Func<EdgeReference, uint> hash = (e) => System.IO.Hashing.XxHash32.HashToUInt32(e.OrientedGeometry.AsBinary());

                    if (_sourceLineType[sourceId] == LineType.Curve) {

                    }
                    else {
                        var linearRing = Reloaded.Factory!.CreateLinearRing(merged.Coordinates);
                        var isCCW = linearRing.IsCCW;
                        merged = linearRing;

                        if (this._sourceLineType[sourceId] == LineType.Exterior) {
                            if (isCCW) {
                                //edges.Reverse();
                                //hash = (e) => System.IO.Hashing.XxHash32.HashToUInt32(e.Edge.Geometry.Reverse().AsBinary());
                                merged = (LinearRing)linearRing.Reverse();
                            }
                        }
                        else if (!isCCW) {
                            //edges.Reverse();
                            //hash = (e) => System.IO.Hashing.XxHash32.HashToUInt32(e.Edge.Geometry.Reverse().AsBinary());
                            merged = (LinearRing)linearRing.Reverse();
                        }
                    }

                    var mergedText = merged.ToText();

                    var sortedlist = new SortedList<int, FeatureRef>();

                    if (sourceId == 1174) System.Diagnostics.Debugger.Break();

                    foreach (var e in edges) {
                        var hashGeometry = hash.Invoke(e);

                        var text = e.OrientedGeometry.ToText().Substring("LINESTRING (".Length).TrimEnd(')');

                        var featureRef = featureRefs[hashGeometry];
                        if (ContainsSegment(mergedText, text))
                            sortedlist.Add(IndexOfSegment(mergedText, text), featureRef);
                        else {
                            text = e.OrientedGeometry.Reverse().ToText().Substring("LINESTRING (".Length).TrimEnd(')');
                            sortedlist.Add(IndexOfSegment(mergedText, text), featureRefs[featureRefs2Reverse[hashGeometry]]);
                        }


                        //var hashOrientedGeometry = System.IO.Hashing.XxHash32.HashToUInt32(e.OrientedGeometry.AsBinary());
                        //refs = [.. refs, featureRefs[hashOrientedGeometry]];
                        //if (sourceId == 1175) {
                        //    if(e.Forward)
                        //        this._interceptor?.Invoke(100, [(e.OrientedGeometry, $"{e.OrientedGeometry.ToText()}")]);
                        //    else
                        //        this._interceptor?.Invoke(100, [((LineString)e.OrientedGeometry.Reverse(), $"{e.OrientedGeometry.ToText()}")]);
                        //}
                    }
                    //if (sourceId == 1175) System.Diagnostics.Debugger.Break();

                    if (sourceId == 1174) {
                        //this._interceptor?.Invoke(6000, [(merged, $"{1174}")]);

                        this._interceptor?.Invoke(100, [.. sortedlist.Values.Select(e => (this._curves[e.Id].LineString, $"Reverse: {e.Reverse}"))]);
                    }

                    var compositecurve = new CompositeCurveFeature([.. sortedlist.Values]);
                    if (!this._compositecurves.ContainsKey(compositecurve.Id)) {
                        this._compositecurves.Add(compositecurve.Id, compositecurve);

                        featureRefs.Add(compositecurve.Id, new FeatureRef {
                            Id = compositecurve.Id,
                            Reverse = false,
                        });
                    }
                    sourceId2FeatureRef.Add(sourceId, compositecurve.Id);
                    if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                        var id = $"C{compositecurve.Id}";
                        this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                    }
                }
                else {
                    var hashOrientedGeometry = System.IO.Hashing.XxHash32.HashToUInt32(edges[0].Edge.Geometry.AsBinary());
                    sourceId2FeatureRef.Add(sourceId, hashOrientedGeometry);
                    if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                        var id = $"C{hashOrientedGeometry}";// e.Forward ? $"C{forward}" : $"RC{forward}";
                        this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                    }
                }


#if null
                if (edges.Count > 1) {

                    //if (sourceId == 1175) {
                    //    this._interceptor?.Invoke(100, [.. edges.Select(e => (e.OrientedGeometry, $"{e.Forward}"))]);
                    //}


                    FeatureRef[] refs = [];
                    //if (edges[0].Edge.Geometry.EqualsExact(edges[^1].Edge.Geometry))
                    //    System.Diagnostics.Debugger.Break();
                    foreach (var e in edges) {
                        //var hashOrientedGeometry = System.IO.Hashing.XxHash32.HashToUInt32(e.OrientedGeometry.AsBinary());
                        //var hashOrientedGeometryReverse = System.IO.Hashing.XxHash32.HashToUInt32(e.OrientedGeometry.Factory.CreateLineString([.. e.OrientedGeometry.Coordinates.Reverse()]).AsBinary());
                        var hashOrientedGeometry = System.IO.Hashing.XxHash32.HashToUInt32(e.Edge.Geometry.AsBinary());
                        var hashOrientedGeometryReverse = System.IO.Hashing.XxHash32.HashToUInt32(Reloaded.Factory!.CreateLineString([.. e.Edge.Geometry.Coordinates.Reverse()]).AsBinary());

                        var forward = hashOrientedGeometry;// e.Forward ? hashOrientedGeometry : hashOrientedGeometryReverse;
                        var backwards = hashOrientedGeometryReverse;// e.Forward ? hashOrientedGeometryReverse : hashOrientedGeometry;

                        //if (forward == 3080250604 || backwards == 3080250604) System.Diagnostics.Debugger.Break();
                        //if (forward == 7034141738638826682 || backwards == 7034141738638826682) System.Diagnostics.Debugger.Break();

                        if (e.Forward) {
                            refs = [.. refs, featureRefs[forward]];
                        }
                        else {
                            refs = [.. refs, featureRefs[backwards]];
                        }
                        if (!this._curves.ContainsKey(forward) && !this._curves.ContainsKey(backwards))
                            this._curves.Add(forward, new CurveFeature(e.Edge.Geometry, forward));
                    }

                    var isCCW = this._mixedTopologyNetwork.IsCCW(edges);

                    //var lineMerger = new LineMerger();
                    //foreach (var edge in sourceRefs[polygon.Value.ExteriorRing]) {
                    //    lineMerger.Add(edge.OrientedGeometry);
                    //}

                    //var curve = lineMerger.GetMergedLineStrings();
                    //var ring = Reloaded.Factory!.CreateLinearRing(curve[0].Coordinates);

                    if (this._sourceLineType[sourceId] == LineType.Curve) {
                        var compositecurve = new CompositeCurveFeature(refs);
                        if (!this._compositecurves.ContainsKey(compositecurve.Id)) {
                            this._compositecurves.Add(compositecurve.Id, compositecurve);

                            featureRefs.Add(compositecurve.Id, new FeatureRef {
                                Id = compositecurve.Id,
                                Reverse = false,
                            });
                        }
                        sourceId2FeatureRef.Add(sourceId, compositecurve.Id);
                        if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                            var id = $"C{compositecurve.Id}";
                            this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                        }
                    }
                    else {
                        if (this._sourceLineType[sourceId] == LineType.Exterior) {
                            if (isCCW) {
                                refs = [.. refs.Select(e => featureRefs2Reverse[e.Id])];
                            }
                        }
                        else if (!isCCW) {
                            refs = [.. refs.Select(e => featureRefs2Reverse[e.Id])];
                        }
                        var compositecurve = new CompositeCurveFeature(refs);
                        if (!this._compositecurves.ContainsKey(compositecurve.Id)) {
                            this._compositecurves.Add(compositecurve.Id, compositecurve);

                            featureRefs.Add(compositecurve.Id, new FeatureRef {
                                Id = compositecurve.Id,
                                Reverse = false,
                            });
                        }
                        sourceId2FeatureRef.Add(sourceId, compositecurve.Id);
                        if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                            var id = $"C{compositecurve.Id}";
                            this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                        }
                    }
                }
                else {
                    var e = edges[0];
                    //var hashOrientedGeometry = System.IO.Hashing.XxHash32.HashToUInt32(e.OrientedGeometry.AsBinary());
                    //var hashOrientedGeometryReverse = System.IO.Hashing.XxHash32.HashToUInt32(e.OrientedGeometry.Factory.CreateLineString([.. e.OrientedGeometry.Coordinates.Reverse()]).AsBinary());
                    var hashOrientedGeometry = System.IO.Hashing.XxHash32.HashToUInt32(e.Edge.Geometry.AsBinary());
                    var hashOrientedGeometryReverse = System.IO.Hashing.XxHash32.HashToUInt32(Reloaded.Factory!.CreateLineString([.. e.Edge.Geometry.Coordinates.Reverse()]).AsBinary());

                    var forward = hashOrientedGeometry;// e.Forward ? hashOrientedGeometry : hashOrientedGeometryReverse;
                    var backwards = hashOrientedGeometryReverse;// e.Forward ? hashOrientedGeometryReverse : hashOrientedGeometry;

                    //if (forward == 1686313706 || backwards == 1686313706) System.Diagnostics.Debugger.Break();

                    if (!this._curves.ContainsKey(forward) && !this._curves.ContainsKey(backwards))
                        this._curves.Add(forward, new CurveFeature(e.Edge.Geometry, forward));

                    sourceId2FeatureRef.Add(sourceId, e.Forward ? forward : backwards);
                    if (this._featureMapperLineStrings.ContainsValue(sourceId)) {
                        var id = e.Forward ? $"C{forward}" : $"RC{forward}";
                        this._mapping.Add(this._featureMapperLineStrings.Single(e => e.Value == sourceId).Key, id);
                    }
                }
#endif
            }


            foreach (var polygon in this._featureMapperPolygons) {
                var uid = polygon.Key;

                FeatureRef exteriorRing = featureRefs[sourceId2FeatureRef[polygon.Value.ExteriorRing]];
                {
                    //if (polygon.Value.ExteriorRing == 1175) System.Diagnostics.Debugger.Break();

                    //var isCCW = this._mixedTopologyNetwork.IsCCW(sourceRefs[polygon.Value.ExteriorRing]);

                    //if (isCCW) {
                    //    exteriorRing = new FeatureRef {
                    //        Id = exteriorRing.Id,
                    //        Reverse = !exteriorRing.Reverse,
                    //    };
                    //}

                    //var lineMerger = new LineMerger();
                    //foreach (var edge in sourceRefs[polygon.Value.ExteriorRing]) {
                    //    lineMerger.Add(edge.OrientedGeometry);
                    //}

                    //var curve = lineMerger.GetMergedLineStrings();
                    //var ring = Reloaded.Factory!.CreateLinearRing(curve[0].Coordinates);
                }

                FeatureRef[] interior = [];
                if (polygon.Value.InteriorRing != default) {
                    for (int i = 0; i < polygon.Value.InteriorRing.Length; i++) {
                        var featureRef = featureRefs[sourceId2FeatureRef[polygon.Value.InteriorRing[i]]];

                        //var isCCW = this._mixedTopologyNetwork.IsCCW(sourceRefs[polygon.Value.InteriorRing[i]]);

                        //if (!isCCW) {
                        //    featureRef = new FeatureRef {
                        //        Id = featureRef.Id,
                        //        Reverse = !featureRef.Reverse,
                        //    };
                        //}

                        //var curve = this._curves[featureRef.Id];

                        //var ring = Reloaded.Factory!.CreateLinearRing(curve.LineString.Coordinates);
                        //if (!ring.IsCCW) {
                        //    featureRef = new FeatureRef {
                        //        Id = featureRef.Id,
                        //        Reverse = !featureRef.Reverse,
                        //    };
                        //}
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

#if null
            //  Create all curves and composite curves
            //  ------------------------------------------------------------------------------------------------
            foreach (var sourceId in this._mixedTopologyNetwork.Sources) {
                foreach (var edge in this._mixedTopologyNetwork.GetMergedEdgeChainFor(sourceId)) {
                    var hash1 = System.IO.Hashing.XxHash64.HashToUInt64(edge.Geometry.AsBinary());
                    var hash2 = System.IO.Hashing.XxHash64.HashToUInt64(edge.Geometry.Factory.CreateLineString([.. edge.Geometry.Coordinates.Reverse()]).AsBinary());

                    if (!featureRefs.ContainsKey(hash1) || !featureRefs.ContainsKey(hash2)) {
                        var featureRef1 = new FeatureRef {
                            Id = hash1,
                            Reverse = false,
                        };
                        featureRefs.Add(featureRef1.Id, featureRef1);
                        featureRef2Curve.Add(featureRef1.Id, edge.Geometry);

                        var featureRef2 = new FeatureRef {
                            Id = hash2,
                            Reverse = true,
                        };
                        featureRefs.Add(featureRef2.Id, featureRef2);
                        featureRef2Curve.Add(featureRef2.Id, edge.Geometry);

                        var curve = new CurveFeature(edge.Geometry, hash1);

                        this._curves.Add(featureRef1.Id, curve);
                        //this._curves.Add(featureRef2.Id, curve);

                        //rc2c.Add($"RC{featureRef1.Id}", $"C{featureRef1.Id}");
                        //rc2c.Add($"RC{featureRef2.Id}", $"C{featureRef1.Id}");

                        rc2c.Add(featureRef1.Id, featureRef1.Id);
                        rc2c.Add(featureRef2.Id, featureRef1.Id);
                    }
                }
            }

            //var curves = new Dictionary<FeatureRef, LineString>();

            var used = new List<FeatureRef>();

            foreach (var sourceId in this._mixedTopologyNetwork.Sources) {
                var chain = this._mixedTopologyNetwork.GetMergedEdgeChainFor(sourceId)!;
                if (!chain.Any()) continue;

                FeatureRef[] refs = [];

                //var linemerger = new LineMerger();

                //if (sourceId == 7) System.Diagnostics.Debugger.Break();

                foreach (var edge in chain) {
                    ulong hash = System.IO.Hashing.XxHash64.HashToUInt64(edge.Geometry.ToBinary());

                    hash = rc2c[hash];  //  remap to non reverse!
                    refs = [.. refs, featureRefs[hash]];

                    //linemerger.Add(edge.Geometry);
                }

                var merger = new LineMerger();
                for (int i = 0; i < chain.Count; i++)
                    merger.Add(chain[i].Geometry);

                var mergedLineStrings = merger.GetMergedLineStrings();
                if (mergedLineStrings.Count > 1) System.Diagnostics.Debugger.Break();
                var merged = (LineString)mergedLineStrings[0];

                bool skip = false;
                foreach (var c in curves) {
                    if (!RingsEqual(c.Value, merged, out bool reverse)) continue;
                    //if (!RingsEqual(c.Value, (LineString)merged[0], out bool reverse)) continue;

                    if (!reverse) {
                        ids.Add(sourceId, c.Key.Reverse ? () => $"RC{c.Key.Id}" : () => $"C{c.Key.Id}");
                    }
                    else {
                        ids.Add(sourceId, !c.Key.Reverse ? () => $"RC{c.Key.Id}" : () => $"C{c.Key.Id}");
                    }
                    source2featureRefs.Add(sourceId, c.Key.Id);
                    skip = true;
                    continue;
                }
                if (skip) continue;

                used.AddRange(refs);

                //if (refs.Select(e => e.Id).ToList().Contains(232626361)) System.Diagnostics.Debugger.Break();

                if (refs.Length > 1) {
                    var mergedText = merged.ToText();

                    var sortedlist = new SortedList<int, FeatureRef>();

                    foreach (var e in refs) {
                        var curve = this._curves[rc2c[e.Id]];

                        var text = curve.LineStringText.Substring("LINESTRING (".Length).TrimEnd(')');

                        if (ContainsSegment(mergedText, text))
                            sortedlist.Add(IndexOfSegment(mergedText, text), e);
                        else {
                            text = curve.LineStringReverseText.Substring("LINESTRING (".Length).TrimEnd(')');
                            sortedlist.Add(IndexOfSegment(mergedText, text), new FeatureRef {
                                Id = e.Id,
                                Reverse = !e.Reverse,
                            });
                        }
                    }
                    //int i = 0;
                    //var sortedlist = new SortedList<int, FeatureRef>(refs.ToDictionary(e => i++, e => e));

                    //var _curves = refs.Skip(0).Select(e => this._curves[rc2c[e.Id]]);

                    //sortedlist.Add(0, refs[0]);
                    //var edge = this._curves[rc2c[refs[0].Id]];
                    //for (int i = 1; i < refs.Length; i++) {
                    //    var next = _curves.Single(e => e.LineString.StartPoint.Equals(edge.LineString.EndPoint));


                    //}

                    var compositecurve = new CompositeCurveFeature([.. sortedlist.Values]);
                    if (!this._compositecurves.ContainsKey(compositecurve.Id))
                        this._compositecurves.Add(compositecurve.Id, compositecurve);

                    ids.Add(sourceId, () => $"C{compositecurve.Id}");
                    source2featureRefs.Add(sourceId, compositecurve.Id);

                    if (!featureRefs.ContainsKey(compositecurve.Id)) {
                        var featureRef1 = new FeatureRef {
                            Id = compositecurve.Id,
                            Reverse = false,
                        };
                        featureRefs.Add(compositecurve.Id, featureRef1);

                        curves.Add(featureRef1, merged);
                        //curves.Add(featureRef1, (LineString)merged[0]);
                    }
                }
                else {
                    ids.Add(sourceId, refs[0].Reverse ? () => $"RC{refs[0].Id}" : () => $"C{refs[0].Id}");

                    source2featureRefs.Add(sourceId, refs[0].Id);

                    curves.Add(refs[0], merged);
                    //curves.Add(refs[0], (LineString)merged[0]);
                }

            }

            //  Create mapping
            //  ------------------------------------------------------------------------------------------------
            foreach (var id in this._mixedTopologyNetwork.Sources) {
                if (this._featureMapperLineStrings.ContainsValue(id)) {
                    var linestring = this._featureMapperLineStrings.Single(e => e.Value == id);

                    var uid = linestring.Key;

                    if (!ids.ContainsKey(id)) continue;

                    string _id = ids[id]();

                    //if (_id.StartsWith("RC"))
                    //    _id = rc2c[_id];
                    this._mapping.Add(uid, _id);
                }
            }

            foreach (var polygon in this._featureMapperPolygons) {
                var uid = polygon.Key;

                FeatureRef exteriorRing = featureRefs[source2featureRefs[polygon.Value.ExteriorRing]];
                {
                    var curve = curves[exteriorRing];
                    var ring = curve.Factory.CreateLinearRing(curve.Coordinates);
                    if (ring.IsCCW) {
                        exteriorRing = new FeatureRef {
                            Id = exteriorRing.Id,
                            Reverse = !exteriorRing.Reverse,
                        };
                    }
                }

                FeatureRef[] interior = [];
                if (polygon.Value.InteriorRing != default) {
                    for (int i = 0; i < polygon.Value.InteriorRing.Length; i++) {
                        var featureRef = featureRefs[source2featureRefs[polygon.Value.InteriorRing[i]]];
                        var curve = curves[featureRef];

                        var ring = curve.Factory.CreateLinearRing(curve.Coordinates);
                        if (!ring.IsCCW) {
                            featureRef = new FeatureRef {
                                Id = featureRef.Id,
                                Reverse = !featureRef.Reverse,
                            };
                        }
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

            //if (this._mapping.Any(e => e.Value.StartsWith("RC"))) System.Diagnostics.Debugger.Break();

            ulong[] _ = [];
            foreach (var e in used) {
                if (e.Reverse) {
                    _ = [.. _, rc2c[e.Id]];
                }
                else
                    _ = [.. _, e.Id];
            }

            this._curves = this._curves.Where(e => _.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value);
#endif
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

        private Dictionary<int, LineType> _sourceLineType = [];

        private ITopologyBuilder AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves, bool isTopology) {
            int[] checks = [];
            //checks = [1546];
            //checks = [577];
            //checks = [97];
            //checks = [648, 1280, 2606, 2741, 1026, 1728, 1040, 1730, 1028, 1727, 2607, 1056, 1725, 1063, 1726, 1384, 2845, 1064, 1724, 504, 1722, 1054, 1721, 455, 1683, 1306, 2609, 2767, 475, 1674, 1283, 2612, 2744, 460, 1672, 452, 1979, 1031, 1977, 457, 1970, 462, 1939, 545, 1924, 1081, 1937, 1082, 1940, 1084, 1941, 1087, 1933, 1088, 1947, 1089, 1950, 1091, 1959, 1092, 1951, 1094, 1957, 1096, 1958, 1098, 1952, 1100, 1963, 1102, 1967, 1105, 1980, 1107, 1671, 1109, 1673, 1111, 1693, 1114, 1692, 1116, 1686, 1118, 1700, 1119, 1698, 1122, 1699, 2604, 2605, 3067, 3084, 3085, 3088, 3716, 4114, 4117, 1154, 1910, 1211, 2674, 1086, 1922, 1083, 1915, 1079, 1909, 535, 1896, 530, 1899, 1898, 1061, 1888, 469, 1893, 1328, 2621, 2789, 490, 1907, 1885, 483, 1884, 1047, 1883, 480, 1882, 1044, 1881, 1043, 2120, 1127, 1880, 3732, 563, 1712, 3731, 1150, 1840];
            //checks = [93, 2336, 3088, 3590, 3628, 1584, 3040, 3683, 3732];
            //checks = [595];
            //checks = [7, 187, 383, 607, 622, 723, 742, 755, 772, 407, 718, 734, 758, 419, 782, 969, 888, 392, 701, 558, 1157, 586, 587, 602, 608, 1163, 1179, 908, 914, 915, 211, 365, 911, 769, 797, 850, 729, 736, 843, 961, 875, 998, 854, 757, 1164, 1171, 1174, 738, 609, 154, 118, 1165, 1177, 1172, 1175, 1178, 773, 180, 750, 416, 390, 754, 420, 385, 417, 716, 359, 362, 614, 424, 615, 896, 882, 740, 415, 418, 761, 374, 714, 405, 776, 753, 735, 400, 703, 422, 398, 715, 368, 395, 698, 382, 770, 376, 713, 421, 414, 707, 401, 375, 710, 397, 372, 721, 386, 495, 402, 455, 391, 442, 393, 460, 364, 1014, 520, 220, 423, 941, 440, 728, 360, 508, 1168, 110, 104, 143, 185, 141, 124, 77, 369, 123, 216, 756, 27, 215, 819, 730, 428, 412, 367, 704, 534, 403, 370, 699, 363, 805, 907, 411, 705, 358, 379, 695, 380, 806, 752, 749, 521, 1003, 446, 478, 67, 410, 413, 722, 371, 790, 473, 158, 171, 81, 186, 408, 533, 763, 766, 396, 388, 696, 399, 378, 717, 409, 406, 709];
            //checks = [2, 87];            

            foreach (var surface in surfaces) {
                //if (surface.UID.EndsWith("F10400040142")) System.Diagnostics.Debugger.Break();
                if (surface.UID.EndsWith("F10400040081")) System.Diagnostics.Debugger.Break();

                var idExteriorRing = this._mixedTopologyNetwork.AddLineString(surface.ExteriorRing);

                _sourceLineType.Add(idExteriorRing, LineType.Exterior);

                var idInteriorRings = new int[0];
                foreach (var interior in surface.InteriorRings) {
                    var id = this._mixedTopologyNetwork.AddLineString(interior);
                    idInteriorRings = [.. idInteriorRings, id];

                    _sourceLineType.Add(id, LineType.Interior);

                    //checks_linestrings = [.. checks_linestrings, interior.ToText()];
                    if (checks.Contains(id)) {
                        checks_linestrings = [.. checks_linestrings, interior.ToText()];
                    }
                }

                //checks_linestrings = [.. checks_linestrings, surface.ExteriorRing.ToText()];
                if (checks.Contains(idExteriorRing)) {
                    checks_linestrings = [.. checks_linestrings, surface.ExteriorRing.ToText()];
                    //System.Diagnostics.Debugger.Break();
                }

                var p = new PolygonSource(idExteriorRing, idInteriorRings);
                this._featureMapperPolygons.Add(surface.Name, p);
            }

            foreach (var curve in curves) {
                //if (curve.UID.EndsWith("10100023030")) System.Diagnostics.Debugger.Break();

                var id = this._mixedTopologyNetwork.AddLineString(curve.LineString);
                if (id < 0) continue;
                _sourceLineType.Add(id, LineType.Curve);

                //checks_linestrings = [.. checks_linestrings, curve.LineString.ToText()];
                if (checks.Contains(id)) {
                    checks_linestrings = [.. checks_linestrings, curve.LineString.ToText()];
                    //System.Diagnostics.Debugger.Break();
                }

                this._featureMapperLineStrings.Add(curve.Name, id);
            }

            return this;
        }

        private static bool RingsEqual(LineString a, LineString b, out bool reverse) {
            reverse = false;

            var coordsA = a.Coordinates.Take(a.Coordinates.Length - 1).ToArray();
            var coordsB = b.Coordinates.Take(b.Coordinates.Length - 1).ToArray();

            if (coordsA.Length != coordsB.Length) return false;

            int n = coordsA.Length;

            for (int dir = 0; dir < 2; dir++) {
                var seqB = dir == 0 ? coordsB : coordsB.Reverse().ToArray();

                for (int offset = 0; offset < n; offset++) {
                    bool match = true;
                    for (int i = 0; i < n; i++) {
                        var ca = coordsA[i];
                        var cb = seqB[(i + offset) % n];
                        if (ca.X != cb.X || ca.Y != cb.Y) {
                            match = false;
                            break;
                        }
                    }
                    if (match) return true;
                }
                reverse = true;
            }

            return false;
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
    }
}