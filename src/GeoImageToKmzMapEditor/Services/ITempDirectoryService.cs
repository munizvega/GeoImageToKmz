namespace TifToKmzMapEditor.Services;

public interface ITempDirectoryService
{
    string CustomTempDirectory { get; set; }
    string EffectiveTempDirectory { get; }
    bool IsCustomDirectoryConfigured { get; }
    void SetCustomTempDirectory(string? path);
    void ResetToDefault();
    long GetTempDirectorySize();
    (int deletedFiles, int deletedDirs, long freedBytes) ClearTempDirectory();
}
