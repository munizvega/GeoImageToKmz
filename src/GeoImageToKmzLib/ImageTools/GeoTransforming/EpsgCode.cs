namespace TifToKmz.ImageTools.GeoTransforming;

/// <summary>
/// European Petroleum Survey Group codes.
/// Here you can some of the most common codes used in WMS (Web Map Service) for geographic transformations.
/// See https://spatialreference.org/ref/ to search new ones (e.g. EPSG:4326 for WGS84 https://spatialreference.org/ref/epsg/4326/).
/// </summary>
public enum EpsgCode
{
    None,
    Wgs84 = 4326,
    Etrs89H28N = 25828,
    Etrs89H29N = 25829,
    Etrs89H30N = 25830,
    Etrs89H31N = 25831
}
