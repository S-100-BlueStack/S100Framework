namespace S100Framework.REST.Models;

/// <summary>
/// Defines supported append source formats for item- and upload-based append operations.
/// </summary>
public enum FeatureServiceAppendSourceFormat
{
    /// <summary>
    /// A SQLite database source.
    /// </summary>
    Sqlite = 0,

    /// <summary>
    /// An OGC GeoPackage source.
    /// </summary>
    GeoPackage = 1,

    /// <summary>
    /// An Esri shapefile source.
    /// </summary>
    ShapeFile = 2,

    /// <summary>
    /// An Esri file geodatabase source.
    /// </summary>
    FileGeodatabase = 3,

    /// <summary>
    /// An ArcGIS feature collection source.
    /// </summary>
    FeatureCollection = 4,

    /// <summary>
    /// A GeoJSON source.
    /// </summary>
    GeoJson = 5,

    /// <summary>
    /// A comma-separated values source.
    /// </summary>
    Csv = 6,

    /// <summary>
    /// A Microsoft Excel workbook source.
    /// </summary>
    Excel = 7,

    /// <summary>
    /// Another ArcGIS feature service source.
    /// </summary>
    FeatureService = 8,

    /// <summary>
    /// A protocol buffer binary source.
    /// </summary>
    Pbf = 9
}