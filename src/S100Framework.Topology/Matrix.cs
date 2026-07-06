using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Operation.Overlay.Snap;
using NetTopologySuite.Precision;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using IO = System.IO;

namespace S100FC.Topology
{
    public class FeatureRef
    {
        public UInt64 Id { get; init; }
        public bool Reverse { get; init; } = false;
    }

    public abstract class FeatureType
    {
        private static UInt64 counter = 1;

        public UInt64 Id { get; init; } = Interlocked.Increment(ref FeatureType.counter);
    }

    public class CurveFeature : FeatureType
    {
        public CurveFeature(LineString lineString, ulong hash) {
            this.LineString = lineString;
            this.LineStringReverse = lineString.Factory.CreateLineString([.. lineString.Coordinates.Reverse()]);

            this.LineStringText = lineString.ToString();
            this.LineStringReverseText = this.LineStringReverse.ToString();
            base.Id = hash;

            //if (base.Id == 52672787 || base.Id == 3587297466) System.Diagnostics.Debugger.Break();
            if (base.Id == 2933884953) System.Diagnostics.Debugger.Break();
        }

        public CurveFeature(LineString lineString) {
            this.LineString = lineString;
            this.LineStringReverse = lineString.Factory.CreateLineString([.. lineString.Coordinates.Reverse()]);

            this.LineStringText = lineString.ToString();
            this.LineStringReverseText = this.LineStringReverse.ToString();

            //base.Id = System.IO.Hashing.XxHash64.HashToUInt64(LineString.ToBinary());
            base.Id = System.IO.Hashing.XxHash32.HashToUInt32(this.LineString.ToBinary());

            //if (base.Id == 11123348237682635517 || base.Id == 8819474955002669271) System.Diagnostics.Debugger.Break();
        }

        public LineString LineString { get; set; }

        public LineString LineStringReverse { get; set; }

        public string LineStringText { get; init; }
        public string LineStringReverseText { get; init; }

        public bool Equals(CurveFeature lineString) {
            if (lineString.LineStringText.Equals(this.LineStringText))
                return true;
            return false;
        }

        public bool Equals(LineString lineString) {
            if (lineString.ToString().Equals(this.LineStringText))
                return true;
            return false;
        }

        public override bool Equals(object? obj) {
            if (obj is CurveFeature curve)
                return (this.Equals(curve));
            if (obj is LineString lineString)
                return (this.Equals(lineString));
            return base.Equals(obj);
        }

        public override int GetHashCode() {
            return (int)System.IO.Hashing.XxHash32.HashToUInt32(this.LineString.ToBinary());
        }
    }

    public class CompositeCurveFeature : FeatureType
    {
        public CompositeCurveFeature(FeatureRef[] curves) {
            this.Curves = curves;

            //base.Id = System.IO.Hashing.XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(string.Join(',', curves.Select(e => e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"))));
            base.Id = System.IO.Hashing.XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(string.Join(',', curves.Select(e => e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"))));

            if (base.Id == 46947589) System.Diagnostics.Debugger.Break();
        }

        public FeatureRef[] Curves { get; init; } = [];
    }

    public class SurfaceFeature : FeatureType
    {
        public required FeatureRef Exterior { get; init; }

        public FeatureRef[]? Interior { get; set; } = default;

        public string? Ref { get; init; } = default;

        public LineString? LineString { get; set; } = default;

        public ICollection<UInt64>? Masks1 { get; set; } = default;
        public ICollection<UInt64>? Masks2 { get; set; } = default;
    }

    public record Polyline(long ObjectId, string Name, string Code, LineString LineString, string UID);

    public record Polygon(long ObjectId, string Name, string Code, LineString ExteriorRing, LineString[] InteriorRings) : Polyline(ObjectId, Name, Code, ExteriorRing, Name);


    //public class CurveContainer
    //{
    //    private readonly Dictionary<UInt64, CurveFeature> _feature = [];
    //    private readonly Dictionary<ulong, (UInt64 Id, bool Reverse)> _keys = [];

    //    public ICollection<CurveFeature> CurveFeatures => this._feature.Values;

    //    public (UInt64 Id, bool Reverse) AddOrGet(LineString lineString) {
    //        var keyStraight = System.IO.Hashing.XxHash3.HashToUInt64(lineString.AsBinary());
    //        var keyReverse = System.IO.Hashing.XxHash3.HashToUInt64(lineString.Reverse().AsBinary());

    //        var curve = new CurveFeature(lineString);

    //        lock (this) {
    //            if (this._keys.ContainsKey(keyStraight)) {
    //                var value = this._keys[keyStraight];
    //                return (value.Id, value.Reverse);
    //            }
    //            this._feature.Add(curve.Id, curve);
    //            this._keys.Add(keyStraight, (curve.Id, false));
    //            this._keys.Add(keyReverse, (curve.Id, true));

    //            return (curve.Id, false);
    //        }
    //    }
    //}

    public class CompositeCurveContainer
    {
        private readonly Dictionary<UInt64, CompositeCurveFeature> _feature = [];
        private readonly Dictionary<string, (UInt64 Id, bool Reverse)> _keys = [];

        public ICollection<CompositeCurveFeature> CompositeCurveFeatures => this._feature.Values;

        public (UInt64 Id, bool Reverse) AddOrGet(IList<FeatureRef> sortedList) {
            var keyStraight = string.Join(',', sortedList.Select(e => e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"));
            var keyReverse = string.Join(',', sortedList.Reverse().Select(e => !e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"));

            var compositeCurve = new CompositeCurveFeature([.. sortedList]) {
            };

            lock (this) {
                if (this._keys.ContainsKey(keyStraight)) {
                    var value = this._keys[keyStraight];
                    return (value.Id, value.Reverse);
                }
                this._feature.Add(compositeCurve.Id, compositeCurve);
                this._keys.Add(keyStraight, (compositeCurve.Id, false));
                this._keys.Add(keyReverse, (compositeCurve.Id, true));

                return (compositeCurve.Id, false);
            }
        }
    }

    public interface ITopologyBuilder
    {
        ITopologyBuilder AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<S100FC.Topology.Polyline> curves);
        ITopologyBuilder AddNavigationalFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<S100FC.Topology.Polyline> curves);

#if Singletons
        ITopologyBuilder AddSingletonFeatures(ICollection<S100FC.Topology.Polyline> curves);
#endif

        GeometryPrecisionReducer Reducer { get; }

        IMatrix BuildTopology();
    }

    public interface IMatrix
    {
        IEnumerable<CurveFeature> Curves { get; }

        IEnumerable<CompositeCurveFeature> CompositeCurves { get; }

        IEnumerable<SurfaceFeature> Surfaces { get; }

        IDictionary<string, string> MappingFOID { get; }
    }

    public class Matrix : ITopologyBuilder, IMatrix
    {
        public static ParallelOptions ParallelOptions { get; set; } = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount > 8 ? 8 : Environment.ProcessorCount };

        public static GeometryFactory Factory { get; set; } = new GeometryFactory(new PrecisionModel(100000000), srid: 4326);

        public static string[] Mask1FeatureTypes { get; set; } = ["DataCoverage"];

        [Obsolete("Use DE9IM_Contains")]
        public const string DE9IM = DE9IM_Contains;

        public const string DE9IM_Contains = "T*****FF*";

        public const string DE9IM_Crosses = "T*T***T**";

        public const string DE9IM_Equals = "T*F**FFF*";


        protected Matrix() {
            //  Default protected constructor            
        }

        private Action<int, ICollection<(LineString lineString, string message)>>? _interceptor = default;

        private readonly ConcurrentBag<(string Name, IEnumerable<LineString> ExteriorRing, List<IEnumerable<LineString>> InteriorRings)> _bagPolygons = [];

        private readonly ConcurrentBag<(string Name, LineString LineString, IEnumerable<LineString> LineStrings)> _bagPolylines = [];

        private readonly ConcurrentDictionary<string, string> _mapping = new ConcurrentDictionary<string, string>();

        private readonly ConcurrentBag<SurfaceFeature> _bagSurfaces = [];

        private IDictionary<string, List<LineString>>? _featureToEdges = new Dictionary<string, List<LineString>>();

        private IList<S100FC.Topology.Polygon> _surfacesTopology = [];
        private IList<S100FC.Topology.Polyline> _curvesTopology = [];

        private IList<S100FC.Topology.Polygon> _surfacesNavigational = [];
        private IList<S100FC.Topology.Polyline> _curvesNavigational = [];

#if Singletons
        private ICollection<S100FC.Topology.Polyline> _curvesSingleton = [];
#endif
        private readonly ConcurrentDictionary<ulong, (FeatureRef fetureRef, CurveFeature curve)> _hashing = new ConcurrentDictionary<ulong, (FeatureRef fetureRef, CurveFeature curve)>();

        //private CurveContainer _curveContainer = new CurveContainer();
        private readonly CompositeCurveContainer _compositeCurveContainer = new CompositeCurveContainer();

        public static ITopologyBuilder CreateMatrix(Action<int, ICollection<(LineString lineString, string message)>>? interceptor = default) {
            return new Matrix() {
                _interceptor = interceptor,
            };
        }

        private static LineString MakePrecise(LineString lineString) {
            for (int i = 0; i < lineString.NumPoints; i++) {
                Matrix.Factory.PrecisionModel.MakePrecise(lineString[i]);
            }
            return lineString;
        }

        private static IList<S100FC.Topology.Polygon> MakePrecise(IList<S100FC.Topology.Polygon> surfaces) {
            foreach (var p in surfaces) {
                MakePrecise(p.ExteriorRing);

                foreach (var interior in p.InteriorRings)
                    MakePrecise(interior);
            }
            return surfaces;
        }

        private static IList<S100FC.Topology.Polyline> MakePrecise(IList<S100FC.Topology.Polyline> curves) {
            foreach (var c in curves) {
                MakePrecise(c.LineString);
            }
            return curves;
        }

        ITopologyBuilder ITopologyBuilder.AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<S100FC.Topology.Polyline> curves) {
            this._surfacesTopology = MakePrecise(surfaces);
            this._curvesTopology = MakePrecise(curves);

            return this;
        }

        ITopologyBuilder ITopologyBuilder.AddNavigationalFeatures(IList<Polygon> surfaces, IList<S100FC.Topology.Polyline> curves) {
            this._surfacesNavigational = MakePrecise(surfaces);
            this._curvesNavigational = MakePrecise(curves);

            return this;
        }

        GeometryPrecisionReducer ITopologyBuilder.Reducer => new GeometryPrecisionReducer(Factory.PrecisionModel);

#if Singletons
        ITopologyBuilder ITopologyBuilder.AddSingletonFeatures(IList<Polyline> curves) {
            this._curvesSingleton = MakePrecise(curves);

            return this;
        }
#endif

        IMatrix ITopologyBuilder.BuildTopology() {
            IEnumerable<S100FC.Topology.Polygon> surfaces = Enumerable.Empty<S100FC.Topology.Polygon>();
            IEnumerable<S100FC.Topology.Polyline> curves = Enumerable.Empty<S100FC.Topology.Polyline>();

            {
                string[] edgeFeatureNames = ["DataCoverage"];
                //string[] edgeFeatureNames = ["DataCoverage", "InformationArea", "MagneticVariation", "NavigationalSystemOfMarks","SoundingDatum","VerticalDatumOfData", "AdministrationArea", "SeaAreaNamedWaterArea"];
                string[] contourFeatureNames = ["DepthContour", "Coastline", "ShorelineConstruction"];

                var edgeFeatures = this._surfacesNavigational.Where(e => edgeFeatureNames.Contains(e.Code)).Select(e => this._surfacesNavigational.IndexOf(e)).ToArray();

                LineString InsertMissingVertices(LineString exteriorRing, LineString lineString) {
                    if (exteriorRing.Disjoint(lineString)) return exteriorRing;
                    if (!exteriorRing.Intersects(lineString)) return exteriorRing;

                    return exteriorRing.InsertMissingVertices(lineString);

                    //if (exteriorRing.Disjoint(lineString)) return exteriorRing;

                    //var result = exteriorRing.LineStringOverlapAnalyzer(lineString);

                    //if (!result.InsertedVertices.Any()) return exteriorRing;

                    //return exteriorRing.Factory.CreateLineString(result.UpdatedACoordinates);
                }

                // this._interceptor?.Invoke(6000, [(before, "F10400000382")]);

                //  Align vertices in topology features
                //for (int i = 0; i < this._surfacesTopology.Count; i++) {
                //    var feature1 = this._surfacesTopology[i];

                //    for (int j = 0; j < this._surfacesTopology.Count; j++) {
                //        if (j == i) continue;
                //        var feature2 = this._surfacesTopology[j];

                //        if ((feature1.UID.Equals("F10800061421") && feature2.UID.Equals("F10800061378")) || (feature1.UID.Equals("F10800061378") && feature2.UID.Equals("F10800061421"))) System.Diagnostics.Debugger.Break();

                //        var line2UniquePart = SnapIfNeededOverlayOp.Intersection(feature1.ExteriorRing, feature2.ExteriorRing);

                //        if (line2UniquePart.IsEmpty) continue;

                //        if (line2UniquePart is GeometryCollection collection) {
                //            continue;
                //        }

                //        if (line2UniquePart is LineString lineString) {
                //            this._surfacesTopology[i] = feature1 with {
                //                ExteriorRing = InsertMissingVertices(feature1.ExteriorRing, lineString),
                //            };
                //        }
                //        else if (line2UniquePart is MultiLineString multiLine) {
                //            foreach (var sub in multiLine.Cast<LineString>()) {
                //                this._surfacesTopology[i] = feature1 with {
                //                    ExteriorRing = InsertMissingVertices(feature1.ExteriorRing, sub),
                //                };
                //                feature1 = this._surfacesTopology[i];
                //            }
                //        }
                //        else if (line2UniquePart is Point point) {
                //            ;
                //        }
                //        else if (line2UniquePart is MultiPoint multiPoint) {
                //            ;
                //        }
                //        else
                //            System.Diagnostics.Debugger.Break();
                //    }
                //}


                //  Insert vertices in edge features
                for (int p = 0; p < this._surfacesTopology.Count; p++) {
                    var feature = this._surfacesTopology[p];
                    if (edgeFeatureNames.Contains(feature.Code)) continue;

                    for (int i = 0; i < edgeFeatures.Length; i++) {
                        var edgeFeature = this._surfacesNavigational[edgeFeatures[i]];

                        var line2UniquePart = SnapIfNeededOverlayOp.Intersection(feature.ExteriorRing, edgeFeature.ExteriorRing);

                        if (line2UniquePart.IsEmpty) continue;

                        Geometry[] lineStrings = [line2UniquePart];

                        if (line2UniquePart is GeometryCollection collection) {
                            var _ = collection.OfType<LineString>();
                            if (!_.Any()) continue;
                            lineStrings = [.. _];
                        }

                        foreach (var _ in lineStrings) {
                            if (_ is LineString lineString) {
                                this._surfacesNavigational[edgeFeatures[i]] = edgeFeature with {
                                    ExteriorRing = InsertMissingVertices(edgeFeature.ExteriorRing, lineString),
                                };
                            }
                            else if (_ is MultiLineString multiLine) {
                                foreach (var sub in multiLine.Cast<LineString>()) {
                                    this._surfacesNavigational[edgeFeatures[i]] = edgeFeature with {
                                        ExteriorRing = InsertMissingVertices(edgeFeature.ExteriorRing, sub),
                                    };
                                    edgeFeature = this._surfacesNavigational[edgeFeatures[i]];
                                }
                            }
                            else if (_ is Point point) {
                                ;
                            }
                            else if (_ is MultiPoint multiPoint) {
                                ;
                            }
                            else
                                System.Diagnostics.Debugger.Break();
                        }
                    }
                }
                for (int p = 0; p < this._surfacesNavigational.Count; p++) {
                }


                for (int c = 0; c < this._curvesTopology.Count; c++) {
                    var contourFeature = this._curvesTopology[c];
                    if (!contourFeatureNames.Contains(contourFeature.Code)) continue;

                    for (int i = 0; i < this._surfacesTopology.Count; i++) {
                        var feature = this._surfacesTopology[i];

                        //if (contourFeature.UID.Equals("F10500048440") && (feature.UID.Equals("F10800061421") || feature.UID.Equals("F10800061378"))) System.Diagnostics.Debugger.Break();

                        var line2UniquePart = SnapIfNeededOverlayOp.Intersection(contourFeature.LineString, feature.ExteriorRing);

                        if (line2UniquePart.IsEmpty) continue;

                        if (line2UniquePart is GeometryCollection collection) {
                            continue;
                        }

                        if (line2UniquePart is LineString lineString) {
                            this._surfacesTopology[i] = feature with {
                                ExteriorRing = InsertMissingVertices(feature.ExteriorRing, lineString),
                            };
                        }
                        else if (line2UniquePart is MultiLineString multiLine) {
                            foreach (var sub in multiLine.Cast<LineString>()) {
                                this._surfacesTopology[i] = feature with {
                                    ExteriorRing = InsertMissingVertices(feature.ExteriorRing, sub),
                                };
                                feature = this._surfacesTopology[i];
                            }
                        }
                        else if (line2UniquePart is Point point) {
                            ;
                        }
                        else if (line2UniquePart is MultiPoint multiPoint) {
                            ;
                        }
                        else
                            System.Diagnostics.Debugger.Break();
                    }
                }

#if null
                //  Update thouching border features
                foreach (var f in features) {
                    for (int i = 0; i < this._surfacesTopology.Count; i++) {
                        var _ = this._surfacesTopology[i];
                        if (borderFeatures.Contains(_.Code)) continue;
                        if (_.ExteriorRing.Disjoint(f.ExteriorRing)) continue;
                        if (!_.ExteriorRing.Intersects(f.ExteriorRing)) continue;
                        try {
                            // 1. Use snapped difference to get only line2's unique part
                            var line2UniquePart = SnapIfNeededOverlayOp.Difference(f.ExteriorRing, _.ExteriorRing);

                            // 2. Also get line1's full extent (snapped union handles the noding)
                            var merged = SnapIfNeededOverlayOp.Union(_.ExteriorRing, line2UniquePart);

                            // 3. LineMerger stitches the noded segments together
                            var merger = new LineMerger();
                            merger.Add(merged);


                            //var lineString = InsertMissingVertices(_.ExteriorRing, f.ExteriorRing);

                            //var merger = new LineMerger();
                            //merger.Add(_.ExteriorRing);
                            //merger.Add(_.ExteriorRing.Intersection(f.ExteriorRing));

                            var lineString = merger.GetMergedLineStrings();

                            Debug.Assert(lineString.Count == 1);

                            this._surfacesTopology[i] = this._surfacesTopology[i] with {
                                ExteriorRing = (LineString)lineString[0],
                            };
                        }
                        catch (NetTopologySuite.Geometries.TopologyException) {
                            continue;
                        }
                    }
                    for (int i = 0; i < this._surfacesNavigational.Count; i++) {
                        var _ = this._surfacesNavigational[i];
                        if (borderFeatures.Contains(_.Code)) continue;
                        if (_.ExteriorRing.Disjoint(f.ExteriorRing)) continue;
                        if (!_.ExteriorRing.Intersects(f.ExteriorRing)) continue;

                        //if (!_.UID.Equals("F10400040314")) {
                        //    continue;
                        //    //    this._interceptor?.Invoke(6000, [(this._surfacesNavigational[i].ExteriorRing, this._surfacesNavigational[i].UID), (InsertMissingVertices(_.ExteriorRing, f.ExteriorRing), "")]);
                        //    System.Diagnostics.Debugger.Break();
                        //}
                        //if (_.UID.Equals("F10400038124")) {
                        //    System.Diagnostics.Debugger.Break();
                        //}
                        try {
                            // 1. Use snapped difference to get only line2's unique part
                            var line2UniquePart = SnapIfNeededOverlayOp.Difference(f.ExteriorRing, _.ExteriorRing);

                            // 2. Also get line1's full extent (snapped union handles the noding)
                            var merged = SnapIfNeededOverlayOp.Union(_.ExteriorRing, line2UniquePart);

                            // 3. LineMerger stitches the noded segments together
                            var merger = new LineMerger();
                            merger.Add(merged);


                            //var lineString = InsertMissingVertices(_.ExteriorRing, f.ExteriorRing);

                            //var merger = new LineMerger();
                            //merger.Add(_.ExteriorRing);
                            //merger.Add(_.ExteriorRing.Intersection(f.ExteriorRing));

                            var lineString = merger.GetMergedLineStrings();

                            Debug.Assert(lineString.Count == 1);

                            this._surfacesNavigational[i] = this._surfacesNavigational[i] with {
                                ExteriorRing = (LineString)lineString[0],
                            };
                        }
                        catch (NetTopologySuite.Geometries.TopologyException) {
                            continue;
                        }

                        //var lineString = InsertMissingVertices(_.ExteriorRing, f.ExteriorRing);

                        //var merger = new LineMerger();
                        //merger.Add(_.ExteriorRing);
                        //merger.Add(_.ExteriorRing.Intersection(f.ExteriorRing));

                        //var merged = merger.GetMergedLineStrings();

                        //Debug.Assert(merged.Count == 1);

                        ////this._interceptor?.Invoke(7000, [(f.ExteriorRing, f.Code),(this._surfacesNavigational[i].ExteriorRing, this._surfacesNavigational[i].UID), (InsertMissingVertices(_.ExteriorRing, f.ExteriorRing), "InsertMissingVertices")]);

                        //this._surfacesNavigational[i] = this._surfacesNavigational[i] with {
                        //    ExteriorRing = (LineString)merged[0],
                        //};
                    }
                }
#endif
                ;
            }


            if (this._surfacesTopology.Any() || this._curvesTopology.Any()) {
                surfaces = surfaces.UnionBy(this._surfacesTopology, e => e.Name);
                curves = curves.Union(this._curvesTopology);    //    curves.UnionBy(this._curvesTopology, e => e.Name);
            }

            if (this._surfacesNavigational.Any() || this._curvesNavigational.Any()) {
                surfaces = surfaces.UnionBy(this._surfacesNavigational, e => e.Name);
                curves = curves.Union(this._curvesNavigational);    //  curves.UnionBy(this._curvesNavigational, e => e.Name);
            }

            var mask1Objects = surfaces.Where(e => Matrix.Mask1FeatureTypes.Contains(e.Code)).Select(e => e.Name).Distinct();

            this.BuildSharedEdges([.. surfaces], [.. curves]);



#if Singletons
            if (null != this._curvesSingleton) {
                var endpoints = this._bagPolylines.SelectMany(e => e.LineStrings.Select(f => new Point[] { f.StartPoint, f.EndPoint })).SelectMany(e => e);

                endpoints = endpoints.Union(this._curvesSingleton.SelectMany(e => new Point[] { e.LineString.StartPoint, e.LineString.EndPoint }));

                endpoints = endpoints.Distinct();

                foreach (var curve in this._curvesSingleton) {
                    var points = curve.LineString.Factory.CreateMultiPoint([.. endpoints]);

                    //if (curve.Name.Equals("C1371117")) System.Diagnostics.Debugger.Break();

                    //var points = curve.LineString.Factory.CreateMultiPoint([.. endpoints.Except([curve.LineString.StartPoint, curve.LineString.EndPoint])]);                    

                    //curve.LineString.Normalize();

                    var intersection = curve.LineString.Intersection(points);

                    if (intersection.IsEmpty) {
                        this._bagPolylines.Add((curve.Name, curve.LineString, [curve.LineString]));
                    }
                    else if (intersection is Point point) {
                        var locator = new LocationIndexedLine(curve.LineString);

                        var index = locator.Project(point.Coordinate);

                        var segment1 = (LineString)locator.ExtractLine(locator.StartIndex, index);
                        var segment2 = (LineString)locator.ExtractLine(index, locator.EndIndex);

                        this._bagPolylines.Add((curve.Name, curve.LineString, [segment1, segment2]));
                    }
                    else {
                        var intersectionPoints = (MultiPoint)intersection;

                        var locator = new LocationIndexedLine(curve.LineString);
                        LinearLocation[] index = [];

                        LineString[] segments = [];
                        foreach (Point p in intersectionPoints) {
                            index = [.. index, locator.Project(p.Coordinate)];
                        }

                        if (index.Length == 2 && index[0].SegmentIndex == locator.StartIndex.SegmentIndex && index[1].SegmentIndex == locator.EndIndex.SegmentIndex) {
                            this._bagPolylines.Add((curve.Name, curve.LineString, [curve.LineString]));
                        }
                        else if (index.Length == 2 && index[0].SegmentIndex == locator.EndIndex.SegmentIndex && index[1].SegmentIndex == locator.StartIndex.SegmentIndex) {
                            this._bagPolylines.Add((curve.Name, curve.LineString, [curve.LineString]));
                        }
                        else {
                            var start = locator.StartIndex;

                            foreach (var i in index.OrderBy(e => e.SegmentIndex)) {
                                if (i.SegmentIndex == start.SegmentIndex)
                                    continue;

                                var segment = (LineString)locator.ExtractLine(start, i);
                                segments = [.. segments, segment];
                                start = i;
                            }
                            if (start.SegmentIndex != locator.EndIndex.SegmentIndex) {
                                var segment = (LineString)locator.ExtractLine(start, locator.EndIndex);
                                segments = [.. segments, segment];
                            }
                            this._bagPolylines.Add((curve.Name, curve.LineString, [.. segments]));
                        }
                    }

                    //this._bagPolylines.Add((curve.Name, [curve.LineString]));



                    //var hash = System.IO.Hashing.XxHash3.HashToUInt64(curve.LineString.AsBinary());

                    //var f = new CurveFeature(curve.LineString);
                    //this._hashing.GetOrAdd(hash, (new FeatureRef {
                    //    Id = f.Id,
                    //    Reverse = false,
                    //}, f));
                    //hash = System.IO.Hashing.XxHash3.HashToUInt64(f.LineString.Reverse().AsBinary());
                    //this._hashing.GetOrAdd(hash, (new FeatureRef {
                    //    Id = f.Id,
                    //    Reverse = true,
                    //}, f));

                    //var featureRef = this._hashing[hash];

                    //this._mapping.GetOrAdd(curve.Name, $"C{featureRef.fetureRef.Id}");
                }
            }
#endif
            var mask1Hashes = new List<UInt64>();

            //  Masking
            foreach (var polygon in this._bagPolygons.Where(e => mask1Objects.Contains(e.Name))) {
                foreach (var lineString in polygon.ExteriorRing) {
                    var hash = System.IO.Hashing.XxHash3.HashToUInt64(lineString.AsBinary());

                    var f = new CurveFeature(lineString);
                    var r = this._hashing.GetOrAdd(hash, (new FeatureRef {
                        Id = f.Id,
                        Reverse = false,
                    }, f));
                    mask1Hashes.Add(r.curve.Id);
                    hash = System.IO.Hashing.XxHash3.HashToUInt64(f.LineString.Reverse().AsBinary());
                    r = this._hashing.GetOrAdd(hash, (new FeatureRef {
                        Id = f.Id,
                        Reverse = true,
                    }, f));
                    mask1Hashes.Add(r.curve.Id);
                }
                if (polygon.InteriorRings.Any()) {
                    foreach (var interior in polygon.InteriorRings) {
                        foreach (var lineString in interior) {
                            var hash = System.IO.Hashing.XxHash3.HashToUInt64(lineString.AsBinary());
                            var f = new CurveFeature(lineString);
                            var r = this._hashing.GetOrAdd(hash, (new FeatureRef {
                                Id = f.Id,
                                Reverse = false,
                            }, f));
                            mask1Hashes.Add(r.curve.Id);
                            hash = System.IO.Hashing.XxHash3.HashToUInt64(f.LineString.Reverse().AsBinary());
                            r = this._hashing.GetOrAdd(hash, (new FeatureRef {
                                Id = f.Id,
                                Reverse = true,
                            }, f));
                            mask1Hashes.Add(r.curve.Id);
                        }
                    }
                }
            }

            mask1Hashes = mask1Hashes.DistinctBy(e => e).ToList();

            Parallel.ForEach(this._bagPolygons, ParallelOptions, (polygon) => {
                foreach (var lineString in polygon.ExteriorRing) {
                    var hash = System.IO.Hashing.XxHash3.HashToUInt64(lineString.AsBinary());

                    var f = new CurveFeature(lineString);
                    var r = this._hashing.GetOrAdd(hash, (new FeatureRef {
                        Id = f.Id,
                        Reverse = false,
                    }, f));
                    hash = System.IO.Hashing.XxHash3.HashToUInt64(f.LineString.Reverse().AsBinary());
                    r = this._hashing.GetOrAdd(hash, (new FeatureRef {
                        Id = f.Id,
                        Reverse = true,
                    }, f));
                }
                if (polygon.InteriorRings.Any()) {
                    foreach (var interior in polygon.InteriorRings) {
                        foreach (var lineString in interior) {
                            var hash = System.IO.Hashing.XxHash3.HashToUInt64(lineString.AsBinary());
                            var f = new CurveFeature(lineString);
                            var r = this._hashing.GetOrAdd(hash, (new FeatureRef {
                                Id = f.Id,
                                Reverse = false,
                            }, f));
                            hash = System.IO.Hashing.XxHash3.HashToUInt64(f.LineString.Reverse().AsBinary());
                            r = this._hashing.GetOrAdd(hash, (new FeatureRef {
                                Id = f.Id,
                                Reverse = true,
                            }, f));
                        }
                    }
                }
            });

            //this._interceptor?.Invoke(7000, [.. this._hashing.Where(e => !e.Value.fetureRef.Reverse).Select(e => (e.Value.curve.LineString, $"{e.Key}"))]);

            Parallel.ForEach(this._bagPolylines, ParallelOptions, (Polyline) => {
                foreach (var lineString in Polyline.LineStrings) {
                    var hash = System.IO.Hashing.XxHash3.HashToUInt64(lineString.AsBinary());

                    var f = new CurveFeature(lineString);
                    this._hashing.GetOrAdd(hash, (new FeatureRef {
                        Id = f.Id,
                        Reverse = false,
                    }, f));
                    hash = System.IO.Hashing.XxHash3.HashToUInt64(f.LineString.Reverse().AsBinary());
                    this._hashing.GetOrAdd(hash, (new FeatureRef {
                        Id = f.Id,
                        Reverse = true,
                    }, f));
                }
            });

            //this._interceptor?.Invoke(9000, [.. this._hashing.Select(e => (e.Value.curve.LineString, ""))]);

            //_interceptor?.Invoke(this._hashing.Where(e => !e.Value.fetureRef.Reverse).Select(e => e.Value.curve.LineString).ToList());

            //Func<IEnumerable<LineString>, LinearRingOrientation, bool, Func<string>, (FeatureRef featureRef, ICollection<CurveFeature> masks1)> action = (lineStrings, orientation, allowMultiLineString, lineString) => 

            (FeatureRef featureRef, ICollection<CurveFeature> masks1) action(IEnumerable<LineString> lineStrings, LinearRingOrientation orientation, bool allowMultiLineString, Func<string> lineString, bool debug) {
                lineStrings = lineStrings.Distinct();

                FeatureRef featureRef;
                var masks1 = new List<CurveFeature>();
                var masks2 = new List<CurveFeature>();

                if (lineStrings.Count() == 1) {
                    var l = lineStrings.ElementAt(0);

                    if (l.IsRing && orientation != LinearRingOrientation.DontCare) {
                        var ring = l.Factory.CreateLinearRing(l.Coordinates);

                        l = ring.IsCCW switch {
                            true => orientation == LinearRingOrientation.CCW ? l : (LineString)l.Reverse(),
                            false => orientation == LinearRingOrientation.CW ? l : (LineString)l.Reverse(),
                        };
                    }

                    var hash = this._hashing[IO.Hashing.XxHash3.HashToUInt64(l.AsBinary())];
                    if (mask1Hashes.Contains(hash.curve.Id))
                        masks1.Add(hash.curve);
                    featureRef = hash.fetureRef;

                    //var curve = this._curveContainer.AddOrGet(l);
                    //featureRef = new FeatureRef {
                    //    Id = curve.Id,
                    //    Reverse = curve.Reverse,
                    //};
                }
                else {
                    var lineMerger = new LineMerger();
                    lineMerger.Add(lineStrings);

                    var mergedLineStrings = lineMerger.GetMergedLineStrings();

                    string lineStringText = lineString();// string.Empty;

                    var sortedList = new SortedList<int, FeatureRef>();
                    var sortedLineStrings = new SortedList<int, LineString>();

                    if (string.IsNullOrEmpty(lineStringText)) {
                        if (allowMultiLineString && mergedLineStrings.Count > 1) {
                            var merged = Matrix.Factory.CreateMultiLineString([.. mergedLineStrings.OfType<LineString>()]);
                            lineStringText = merged.ToText();
                        }
                        else {
                            if (mergedLineStrings.Count > 1) {
                                this._interceptor?.Invoke(8001, lineStrings.Select(e => (e, "lineStrings")).ToArray());
                                this._interceptor?.Invoke(8002, mergedLineStrings.Select(e => ((LineString)e, "mergedLineStrings")).ToArray());
                            }

                            Debug.Assert(mergedLineStrings.Count == 1);

                            var merged = (LineString)mergedLineStrings[0];

                            if (merged.IsRing && orientation != LinearRingOrientation.DontCare) {
                                var ring = merged.Factory.CreateLinearRing(merged.Coordinates);

                                merged = ring.IsCCW switch {
                                    true => orientation == LinearRingOrientation.CCW ? merged : (LineString)merged.Reverse(),
                                    false => orientation == LinearRingOrientation.CW ? merged : (LineString)merged.Reverse(),
                                };
                            }

                            lineStringText = merged.ToText();
                        }
                    }
                    else if (allowMultiLineString && mergedLineStrings.Count > 1) {
                        //this._interceptor?.Invoke(8001, [.. lineStrings.Select(e=>(e,""))]);

                        for (int i = 0; i < lineStrings.Count(); i++) {
                            var text = lineStrings.ElementAt(i).ToText().Substring("LINESTRING (".Length).TrimEnd(')');
                            if (Matrix.ContainsSegment(lineStringText, text)) {
                                var hash = this._hashing[IO.Hashing.XxHash3.HashToUInt64(lineStrings.ElementAt(i).AsBinary())];

                                if (mask1Hashes.Contains(hash.curve.Id))
                                    masks1.Add(hash.curve);

                                sortedList.Add(i, hash.fetureRef);
                            }
                            else {
                                var reverse = lineStrings.ElementAt(i).Reverse();
                                text = reverse.ToText().Substring("LINESTRING (".Length).TrimEnd(')');

                                var hash = this._hashing[IO.Hashing.XxHash3.HashToUInt64(reverse.AsBinary())];

                                if (mask1Hashes.Contains(hash.curve.Id))
                                    masks1.Add(hash.curve);

                                sortedList.Add(i, hash.fetureRef);
                            }
                        }
                    }

                    if (!sortedList.Any()) {
                        for (int i = 0; i < lineStrings.Count(); i++) {
                            //var curve = this._curveContainer.AddOrGet(lineStrings.ElementAt(i));

                            var text = lineStrings.ElementAt(i).ToText().Substring("LINESTRING (".Length).TrimEnd(')');

                            if (Matrix.ContainsSegment(lineStringText, text)) {
                                //if (lineStringText.Contains(text)) {
                                var hash = this._hashing[IO.Hashing.XxHash3.HashToUInt64(lineStrings.ElementAt(i).AsBinary())];

                                if (mask1Hashes.Contains(hash.curve.Id))
                                    masks1.Add(hash.curve);

                                var index = Matrix.IndexOfSegment(lineStringText, text);
                                Debug.Assert(index >= 0);
                                //sortedList.Add(lineStringText.IndexOf(text), hash.fetureRef);
                                sortedList.Add(index, hash.fetureRef);

                                sortedLineStrings.Add(index, lineStrings.ElementAt(i));
                            }
                            else {
                                var reverse = lineStrings.ElementAt(i).Reverse();
                                text = reverse.ToText().Substring("LINESTRING (".Length).TrimEnd(')');

                                var hash = this._hashing[IO.Hashing.XxHash3.HashToUInt64(reverse.AsBinary())];

                                if (mask1Hashes.Contains(hash.curve.Id))
                                    masks1.Add(hash.curve);

                                var index = Matrix.IndexOfSegment(lineStringText, text);
                                Debug.Assert(index >= 0);
                                //sortedList.Add(lineStringText.IndexOf(text), hash.fetureRef);
                                sortedList.Add(index, hash.fetureRef);

                                sortedLineStrings.Add(index, (LineString)lineStrings.ElementAt(i).Reverse());
                            }
                        }
                    }
                    var compositeExterior = this._compositeCurveContainer.AddOrGet(sortedList.Values);

                    featureRef = new FeatureRef {
                        Id = compositeExterior.Id,
                        Reverse = compositeExterior.Reverse,
                    };

                    if (debug) {
                        this._interceptor?.Invoke(1000, [.. sortedLineStrings.OrderBy(e => e.Key).Select(e => (e.Value, $"{e.Key}"))]);
                    }
                }

                return (featureRef, masks1);
            }

            Parallel.ForEach(this._bagPolygons, ParallelOptions, (polygon) => {
                //var debug = polygon.Name.Equals("F10400000382");

                //if (polygon.Name.Equals("F10400000382")) System.Diagnostics.Debugger.Break();

                if (!polygon.ExteriorRing.Any()) return;

                var exteriorId = action(polygon.ExteriorRing, LinearRingOrientation.Clockwise, false, () => string.Empty, false);

                var surface = new SurfaceFeature() {
                    Ref = polygon.Name,
                    Exterior = exteriorId.featureRef,
                };
                //if (!mask1Objects.Contains(polygon.Name)) {
                if (exteriorId.masks1.Any())
                    surface.Masks1 = [.. exteriorId.masks1.Select(e => e.Id)];
                //}
                if (polygon.InteriorRings.Any()) {
                    var interiorRings = polygon.InteriorRings.Select(e => action(e, LinearRingOrientation.CounterClockwise, false, () => string.Empty, false));
                    surface.Interior = [.. interiorRings.Select(e => e.featureRef)];

                    //  interior ring can't touch bondary!
                    //var masks1 = interiorRings.SelectMany(e => e.masks1).Select(e => e.Id).Distinct();
                    //if (masks1.Any()) {
                    //    if (surface.Masks1 != default && surface.Masks1.Any())
                    //        surface.Masks1 = [.. surface.Masks1, .. masks1];
                    //    else
                    //        surface.Masks1 = [.. masks1];
                    //}
                }
                this._bagSurfaces.Add(surface);
                this._mapping.GetOrAdd(polygon.Name, $"S{surface.Id}");

                //if (polygon.Name.Equals("F10400000382")) {
                //    //this._interceptor?.Invoke(1000, [.. polygon.ExteriorRing.Select(e => (e, "F10400000382"))]);
                //    //System.Diagnostics.Debugger.Break();

                //    var curve = this._compositeCurveContainer.CompositeCurveFeatures.Single(e => e.Id == exteriorId.featureRef.Id);
                //    LineString[] linestrings = [];
                //    foreach (var c in curve.Curves) {
                //        var l = this._hashing.Single(e => e.Value.fetureRef.Id == c.Id && e.Value.fetureRef.Reverse == c.Reverse);
                //        //var hash = this._hashing[c.Id];                        

                //        linestrings = [.. linestrings, l.Value.curve.LineString];
                //    }

                //    this._interceptor?.Invoke(7000, [.. linestrings.Select(e => (e, e.ToText()))]);
                //}
            });

            //ParallelOptions.MaxDegreeOfParallelism = 1;

            Parallel.ForEach(this._bagPolylines, ParallelOptions, (polyline) => {
                if (!polyline.LineStrings.Any()) return;
                //if (polyline.Name.Equals("C1371117")) System.Diagnostics.Debugger.Break();

                var text = polyline.LineString.ToText();

                //if (polyline.Name.Equals("C1373970")) {
                //    System.Diagnostics.Debugger.Break();

                //    var locator = new LocationIndexedLine(polyline.LineString);

                //    foreach (var e in polyline.LineStrings) {
                //        var l = locator.IndicesOf(e);
                //    }
                //}

                text = text.Substring("LINESTRING (".Length).TrimEnd(')');

                if (polyline.LineString.StartPoint == polyline.LineString.EndPoint) {
                    text = text += ", " + string.Join(", ", text.Split(", ").Skip(1));
                }
                else
                    text = text + ", " + text;

                var curveId = action(polyline.LineStrings, LinearRingOrientation.DontCare, true, () => text, false);

                this._mapping.GetOrAdd(polyline.Name, $"C{curveId.featureRef.Id}");
            });

            return this;
        }

        private void BuildSharedEdges(ICollection<S100FC.Topology.Polygon> surfaces, ICollection<S100FC.Topology.Polyline> curves) {
            var minimalEdges = new HashSet<Edge>();

            var edgeToFeatureMap = new Dictionary<Edge, List<string>>();

            void AddLineString(string name, LineString lineString) {
                var coordinates = lineString.Coordinates;
                for (int i = 0; i < coordinates.Length - 1; i++) {
                    var edge = new Edge(coordinates[i], coordinates[i + 1]);
                    minimalEdges.Add(edge);

                    if (!edgeToFeatureMap.ContainsKey(edge)) {
                        edgeToFeatureMap[edge] = [];
                    }
                    edgeToFeatureMap[edge].Add(name);
                }
            }

            foreach (var polygon in surfaces) {
                AddLineString(polygon.Name, polygon.ExteriorRing);
                if (polygon.InteriorRings.Any()) {
                    for (int i = 0; i < polygon.InteriorRings.Count(); i++)
                        AddLineString($"{polygon.Name}i{i}", polygon.InteriorRings[i]);
                }
            }

            foreach (var curve in curves) {
                //if ("F10500105683".Equals(curve.Name)) System.Diagnostics.Debugger.Break();

                AddLineString(curve.Name, curve.LineString);
            }

            //this._interceptor?.Invoke(6000, [.. edgeToFeatureMap.Where(e => e.Value.Contains("F10400000382")).Select(e => (e.Key.LineString, string.Join(',', e.Value)))]);
            //this._interceptor?.Invoke(9002, [.. edgeToFeatureMap.Select(e =>(e.Key.LineString, string.Join(',', e.Value)))]);

            this._featureToEdges = new Dictionary<string, List<LineString>>();

            foreach (var e in edgeToFeatureMap.GroupBy(e => string.Join(',', e.Value))) {
                //if (e.Key.Contains("S1799633")) System.Diagnostics.Debugger.Break();

                //if (e.Any(x => x.Key.LineString.ToText().Equals("LINESTRING (11.7465969 54.8704114, 11.7468865 54.8707256)"))) System.Diagnostics.Debugger.Break();

                var merger = new LineMerger();
                merger.Add(e.Select(x => x.Key.LineString));

                var mergedLineStrings = merger.GetMergedLineStrings().OfType<LineString>();

                var hits = e.Key.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in hits) {
                    if (!this._featureToEdges.ContainsKey(p))
                        this._featureToEdges.Add(p, []);
                    this._featureToEdges[p].AddRange(mergedLineStrings);
                }
            }

            Parallel.For(0, surfaces.Count, Matrix.ParallelOptions, (p) => {
                var surface = surfaces.ElementAt(p);
                IEnumerable<LineString> exteriorRing = this._featureToEdges[surface.Name];
                var interiorRings = new List<IEnumerable<LineString>>();
                for (int i = 0; i < surface.InteriorRings.Count(); i++) {
                    interiorRings.Add(this._featureToEdges[$"{surface.Name}i{i}"]);
                }
                this._bagPolygons.Add((surface.Name, exteriorRing, interiorRings));
            });

            //this._interceptor?.Invoke(6001, [.. this._bagPolygons.Where(e=>e.Name.Equals("F10400000002")).SelectMany(e => e.ExteriorRing.Select(f=>(f, e.Name)))]);

            Parallel.For(0, curves.Count, Matrix.ParallelOptions, (c) => {
                var curve = curves.ElementAt(c);

                IEnumerable<LineString> lineStrings = this._featureToEdges[curve.Name];

                //if (curve.Name.Equals("C1371117")) System.Diagnostics.Debugger.Break();

                //if (lineStrings.Any(e => e.IsRing)) {
                //    LineString[] array = [];
                //    foreach(var l in lineStrings) {
                //        if(!l.IsRing)
                //            array = [.. array,l];
                //        else {
                //            var locator = new LocationIndexedLine(l);

                //            var index = locator.Project(l.GetPointN(l.NumPoints/2).Coordinate);

                //            var segment1 = (LineString)locator.ExtractLine(locator.StartIndex, index);
                //            var segment2 = (LineString)locator.ExtractLine(index, locator.EndIndex);

                //            array = [.. array, segment1,segment2];
                //        }
                //    }
                //    lineStrings= array;
                //}

                this._bagPolylines.Add((curve.Name, curve.LineString, lineStrings));
            });
        }

        IEnumerable<CurveFeature> IMatrix.Curves => this._hashing.Select(e => e.Value.curve).DistinctBy(e => e.Id);
        //IEnumerable<CurveFeature> iMatrix.Curves => this._curveContainer.CurveFeatures; //this._hashing.Select(e => e.Value.curve).DistinctBy(e => e.Id);

        IEnumerable<CompositeCurveFeature> IMatrix.CompositeCurves => this._compositeCurveContainer.CompositeCurveFeatures; // this._bagCompositeCurves.Values;

        IEnumerable<SurfaceFeature> IMatrix.Surfaces => this._bagSurfaces;

        IDictionary<string, string> IMatrix.MappingFOID => this._mapping;

        //string[] IMatrix.MappingFeature(string name) {
        //    return [.. this._mapping.Where(e => e.Key.Equals(name) || e.Key.StartsWith($"{name}:p")).Select(e => e.Value)];
        //}

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

        private class Edge : IEquatable<Edge>
        {
            public NetTopologySuite.Geometries.Coordinate Start { get; }
            public NetTopologySuite.Geometries.Coordinate End { get; }

            public Edge(NetTopologySuite.Geometries.Coordinate p1, NetTopologySuite.Geometries.Coordinate p2) {
                if (p1.CompareTo(p2) < 0) { this.Start = p1; this.End = p2; }
                else { this.Start = p2; this.End = p1; }
            }

            public bool Equals(Edge? other) {
                if (other is null) return false;
                return this.Start.Equals(other.Start) && this.End.Equals(other.End);
            }
            public override bool Equals(object? obj) => this.Equals(obj as Edge);
            public override int GetHashCode() => (this.Start, this.End).GetHashCode();
            public override string ToString() => $"EDGE ({this.Start.X} {this.Start.Y}, {this.End.X} {this.End.Y})";

            public LineString LineString => Matrix.Factory.CreateLineString([this.Start, this.End]);
        }

        public static void AddLineStringsFromGeometry(NetTopologySuite.Geometries.Geometry geometry, List<LineString> targetList) {
            if (geometry is LineString line) {
                if (!line.IsEmpty) {
                    if (!targetList.Any(e => e.EqualsTopologically(line)))
                        targetList.Add(line.RemoveRepeatedVertices());
                }
            }
            else if (geometry is MultiLineString multiLine) {
                foreach (var subLine in multiLine.Geometries.OfType<LineString>()) {
                    if (!subLine.IsEmpty) {
                        if (!targetList.Any(e => e.EqualsTopologically(subLine)))
                            targetList.Add(subLine.RemoveRepeatedVertices());
                    }
                }
            }
            else if (geometry is GeometryCollection collection) // Recursively handle collections if needed
            {
                foreach (var geom in collection.Geometries) {
                    AddLineStringsFromGeometry(geom, targetList);
                }
            }
        }

        public static bool IsGeometryOverlapping(IEnumerable<LineString> lineStrings) {
            //  Validate
            var result = false;
            Parallel.For(0, lineStrings.Count(), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (i) => {
                var boundary1 = lineStrings.ElementAt(i);
                for (var j = 0; j < lineStrings.Count(); j++) {
                    if (j == i) continue;

                    var boundary2 = lineStrings.ElementAt(j);

                    var intersection = boundary1.Intersection(boundary2);

                    if (intersection.IsEmpty)
                        continue;
                    if (intersection is NetTopologySuite.Geometries.Point || intersection is NetTopologySuite.Geometries.MultiPoint) {
                        continue;
                    }
                    else
                        result |= true;
                }
            });
            return result;
        }
    }
}


