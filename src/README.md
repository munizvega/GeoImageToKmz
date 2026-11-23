# GeoImageToKmz

Standalone .NET solution for working with geospatial raster images and exporting map content to KMZ. It contains only the processing library and the interactive map editor; the original console application and `TifToKmzUI` are not included.

## Projects

- `GeoImageToKmzLib`: geospatial image processing, GDAL utilities, geometry, and KMZ export services. Existing `TifToKmz` and `TifToKmzLib` namespaces are retained for source and API compatibility.
- `GeoImageToKmzMapEditor`: Avalonia desktop map editor and viewer, referencing `GeoImageToKmzLib`.

Both projects target .NET 10. Package versions and native GDAL dependencies are the same as in the source projects. The configured GDAL runtime packages cover `linux-x64` and `win-x64` publishing.

## Build and run

From this directory:

```bash
dotnet build GeoImageToKmz.slnx -c Release
dotnet run --project GeoImageToKmzMapEditor/GeoImageToKmzMapEditor.csproj
```

## Publish

Publish for the target operating system and architecture so the matching native GDAL runtime is included:

```bash
dotnet publish GeoImageToKmzMapEditor/GeoImageToKmzMapEditor.csproj -c Release -r linux-x64 --self-contained true -o artifacts/publish/linux-x64
dotnet publish GeoImageToKmzMapEditor/GeoImageToKmzMapEditor.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```