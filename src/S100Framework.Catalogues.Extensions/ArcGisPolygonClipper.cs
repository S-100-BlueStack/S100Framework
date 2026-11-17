#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Geometry;
using NetTopologySuite;
using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;
using EsriGeometry = ArcGIS.Core.Geometry.Geometry;
using EsriMultipart = ArcGIS.Core.Geometry.Multipart;
using EsriMultipoint = ArcGIS.Core.Geometry.Multipoint;
using EsriPolygon = ArcGIS.Core.Geometry.Polygon;
using EsriPolyline = ArcGIS.Core.Geometry.Polyline;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsLocation = NetTopologySuite.Geometries.Location;

namespace SpatialProcessing;

public static class ArcGisPolygonClipper
{
    private static readonly PrecisionModel FloatingPrecision =
        new PrecisionModel(PrecisionModels.Floating);

    // Keep WKB decoding independent of any fixed precision configured elsewhere.
    private static readonly NtsGeometryServices GeometryServices =
        new NtsGeometryServices(FloatingPrecision);

    /// <summary>
    /// Clips a map point or a linear XY polygon, polyline, or multipoint to a polygon mask.
    /// The result has the same ArcGIS geometry type and spatial reference as the source.
    /// Map points are tested in XY and retain their attributes when kept.
    /// </summary>
    public static TGeometry Clip<TGeometry>(TGeometry source, EsriGeometry clipGeometry)
        where TGeometry : EsriGeometry {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(clipGeometry);

        if (source is not (EsriPolygon or EsriPolyline or MapPoint or EsriMultipoint))
            throw new ArgumentException("Expected a polygon, polyline, map point, or multipoint.", nameof(source));

        if (clipGeometry is not EsriPolygon mask)
            throw new ArgumentException("Expected an ArcGIS polygon mask.", nameof(clipGeometry));

        // A single point is either retained as-is or emptied, so no ordinates
        // need to be interpolated and no vertex IDs need to be generated.
        if (source is not MapPoint)
            RequireLinearXY(source, nameof(source));

        if (mask.HasCurves)
            throw new ArgumentException("Expected a polygon mask with straight segments.", nameof(clipGeometry));

        var spatialReference = source.SpatialReference;
        var maskSpatialReference = mask.SpatialReference;
        if (spatialReference is null || maskSpatialReference is null ||
            !spatialReference.IsEqual(maskSpatialReference, false)) {
            throw new ArgumentException(
                "The source and polygon mask must have the same spatial reference.",
                nameof(clipGeometry));
        }

        if (source.IsEmpty)
            return source;

        if (mask.IsEmpty)
            return (TGeometry)CreateEmptyLike(source, spatialReference);

        var engine = GeometryEngine.Instance;
        var reader = new WKBReader(GeometryServices);
        var ntsMask = ReadMultipart(engine, reader, mask, nameof(clipGeometry));
        var maskLocator = new IndexedPointInAreaLocator(ntsMask);

        EsriGeometry result = source switch {
            MapPoint point => IsCovered(maskLocator, point)
                ? point
                : CreateEmptyLike(point, spatialReference),
            EsriMultipoint multipoint => ClipMultipoint(multipoint, maskLocator, spatialReference),
            EsriMultipart multipart => ClipMultipart(
                multipart, ntsMask, maskLocator, spatialReference, engine, reader),
            _ => throw new InvalidOperationException("Unexpected source geometry type.")
        };

        return (TGeometry)result;
    }

    private static EsriGeometry ClipMultipart(
        EsriMultipart source, NtsGeometry mask, IndexedPointInAreaLocator maskLocator,
        SpatialReference spatialReference, IGeometryEngine engine, WKBReader reader) {
        var ntsSource = ReadMultipart(engine, reader, source, nameof(source));

        // Returning the original also preserves ring and path structure on no-op clips.
        if (mask.Covers(ntsSource))
            return source;

        var overlay = new OverlayNG(
            ntsSource, mask, FloatingPrecision, SpatialFunction.Intersection) {
            StrictMode = true
        };

        var clipped = overlay.GetResult();
        var keepResult = !clipped.IsEmpty && clipped.Dimension == ntsSource.Dimension;
        if (keepResult && (!MatchesMultipartType(source, clipped) || !clipped.IsValid))
            throw new InvalidOperationException("Clipping did not produce valid geometry of the source dimension.");

        // A point-only line contact or a line-only polygon contact yields an empty source type.
        var resultCoordinates = keepResult
            ? clipped.Coordinates.Select(point => (point.X, point.Y)).ToHashSet()
            : new HashSet<(double X, double Y)>();

        foreach (var point in ntsSource.Coordinates) {
            if (!resultCoordinates.Contains((point.X, point.Y)) &&
                maskLocator.Locate(point) == NtsLocation.Interior) {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Clipping did not preserve source vertex ({point.X:R}, {point.Y:R})."));
            }
        }

        if (!keepResult)
            return CreateEmptyLike(source, spatialReference);

        var wkb = new WKBWriter().Write(clipped);
        var imported = engine.ImportFromWKB(WkbImportFlags.WkbImportDefaults, wkb, spatialReference);
        if (imported is not EsriMultipart result || result.GeometryType != source.GeometryType)
            throw new InvalidOperationException("ArcGIS did not import the result as the source geometry type.");

        if (!resultCoordinates.SetEquals(result.Points.Select(point => (point.X, point.Y))))
            throw new InvalidOperationException("ArcGIS changed XY vertex positions during WKB import.");

        return result;
    }

    private static EsriMultipoint ClipMultipoint(
        EsriMultipoint source, IndexedPointInAreaLocator maskLocator, SpatialReference spatialReference) {
        // Filtering the original points preserves point order and duplicate coordinates.
        var kept = source.Points.Where(point => IsCovered(maskLocator, point)).ToArray();

        if (kept.Length == source.PointCount)
            return source;

        if (kept.Length == 0)
            return new MultipointBuilderEx(spatialReference).ToGeometry();

        var result = MultipointBuilderEx.CreateMultipoint(kept, spatialReference);
        if (!kept.Select(point => (point.X, point.Y))
            .SequenceEqual(result.Points.Select(point => (point.X, point.Y)))) {
            throw new InvalidOperationException("ArcGIS changed the retained multipoint coordinates or their order.");
        }

        return result;
    }

    private static bool IsCovered(IndexedPointInAreaLocator locator, MapPoint point) {
        // Boundary points belong to the polygon intersection, including hole boundaries.
        return locator.Locate(new Coordinate(point.X, point.Y)) != NtsLocation.Exterior;
    }

    private static EsriGeometry CreateEmptyLike(EsriGeometry source, SpatialReference spatialReference) {
        return source switch {
            EsriPolygon => new PolygonBuilderEx(spatialReference).ToGeometry(),
            EsriPolyline => new PolylineBuilderEx(spatialReference).ToGeometry(),
            MapPoint point => new MapPointBuilderEx(spatialReference) {
                HasZ = point.HasZ,
                HasM = point.HasM,
                HasID = point.HasID
            }.ToGeometry(),
            EsriMultipoint => new MultipointBuilderEx(spatialReference).ToGeometry(),
            _ => throw new InvalidOperationException("Unexpected source geometry type.")
        };
    }

    private static void RequireLinearXY(EsriGeometry geometry, string parameterName) {
        // These values require a separate policy for generated cut vertices.
        if (geometry is EsriMultipart { HasCurves: true } ||
            geometry.HasZ || geometry.HasM || geometry.HasID) {
            throw new ArgumentException(
                "Expected linear XY geometry without Z, M, or vertex IDs.", parameterName);
        }
    }

    private static NtsGeometry ReadMultipart(
        IGeometryEngine engine, WKBReader reader, EsriMultipart source, string parameterName) {
        // The mask defines an XY footprint, so its Z/M values do not take part in clipping.
        const WkbExportFlags exportFlags = WkbExportFlags.WkbExportStripZs |
                                          WkbExportFlags.WkbExportStripMs;
        var wkb = engine.ExportToWKB(exportFlags, source);
        var geometry = reader.Read(wkb);

        if (!MatchesMultipartType(source, geometry) || !geometry.IsValid)
            throw new ArgumentException("Expected a valid polygon or polyline.", parameterName);

        var sourceCoordinates = source.Points.Select(point => (point.X, point.Y)).ToHashSet();
        if (!sourceCoordinates.SetEquals(geometry.Coordinates.Select(point => (point.X, point.Y)))) {
            throw new InvalidOperationException(
                $"WKB conversion changed the XY vertex positions of '{parameterName}'.");
        }

        return geometry;
    }

    private static bool MatchesMultipartType(EsriMultipart source, NtsGeometry geometry) {
        return source is EsriPolygon
            ? geometry is IPolygonal
            : geometry is LineString or MultiLineString;
    }
}
