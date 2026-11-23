# GeoImageToKmz development guide

## Solution structure

- `GeoImageToKmzLib` contains the geospatial processing library, including GDAL-backed raster tools, geometry, and KMZ export services.
- `GeoImageToKmzMapEditor` contains the Avalonia desktop map editor and references the library.
- `GeoImageToKmz.slnx` includes only these two projects. The console application and `TifToKmzUI` remain outside this solution.

The projects target `net10.0`. Keep the existing package versions and GDAL runtime dependencies unchanged. The configured native runtime packages target `linux-x64` and `win-x64`. The existing `TifToKmz` and `TifToKmzLib` code namespaces are intentional and should remain stable.

## Build and publish

Run from the solution directory:

```bash
dotnet build GeoImageToKmz.slnx -c Release
dotnet publish GeoImageToKmzMapEditor/GeoImageToKmzMapEditor.csproj -c Release -r linux-x64 --self-contained true -o artifacts/publish/linux-x64
dotnet publish GeoImageToKmzMapEditor/GeoImageToKmzMapEditor.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

Follow the existing C# conventions in the source projects: four-space indentation, Allman braces, PascalCase public members, and underscore-prefixed camelCase private fields. Do not change SDK or package versions as part of routine maintenance.