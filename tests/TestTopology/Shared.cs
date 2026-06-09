using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestTopology
{
    internal static class Shared
    {
        public static ArcGIS.Core.Geometry.Polyline ConvertToArcGISPolyline(NetTopologySuite.Geometries.LineString ntsLineString, ArcGIS.Core.Geometry.SpatialReference? spatialReference = null) {
            // Build a collection of MapPoints from NTS coordinates
            var points = ntsLineString.Coordinates
                .Select(coord => {
                    // NTS uses Z if it's not NaN
                    if (!double.IsNaN(coord.Z))
                        return MapPointBuilderEx.CreateMapPoint(coord.X, coord.Y, coord.Z, spatialReference);
                    else
                        return MapPointBuilderEx.CreateMapPoint(coord.X, coord.Y, spatialReference);
                })
                .ToList();

            // Create the ArcGIS Polyline from the point collection
            return PolylineBuilderEx.CreatePolyline(points, spatialReference);
        }
    }
}
