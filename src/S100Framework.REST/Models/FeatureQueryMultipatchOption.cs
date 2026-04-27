namespace S100Framework.REST.Models;

/// <summary>
/// Defines how multipatch geometries should be returned from a feature query.
/// </summary>
public enum FeatureQueryMultipatchOption
{
    /// <summary>
    /// Returns the x/y footprint of the multipatch geometry.
    /// </summary>
    XyFootprint = 0,

    /// <summary>
    /// Returns multipatch geometry without material information.
    /// </summary>
    StripMaterials = 1,

    /// <summary>
    /// Returns multipatch geometry with embedded material information.
    /// </summary>
    EmbedMaterials = 2,

    /// <summary>
    /// Returns multipatch geometry with texture references instead of embedded textures.
    /// </summary>
    ExternalizeTextures = 3,

    /// <summary>
    /// Returns the 3D extent footprint of the multipatch geometry when supported by the layer.
    /// </summary>
    Extent = 4
}