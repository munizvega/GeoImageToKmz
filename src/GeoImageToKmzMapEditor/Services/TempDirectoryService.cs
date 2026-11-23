using System.Text.Json;

namespace TifToKmzMapEditor.Services;

public sealed class TempDirectoryService : ITempDirectoryService
{
    private const string _configFileName = "appsettings.editor.json";
    private string _customTempDirectory = string.Empty;

    public string CustomTempDirectory
    {
        get => _customTempDirectory;
        set => SetCustomTempDirectory(value);
    }

    public string EffectiveTempDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_customTempDirectory) && Directory.Exists(_customTempDirectory))
            {
                return _customTempDirectory;
            }

            return Path.Combine(Path.GetTempPath(), "TifToKmzMapEditor");
        }
    }

    public bool IsCustomDirectoryConfigured =>
        !string.IsNullOrWhiteSpace(_customTempDirectory);

    public TempDirectoryService()
    {
        LoadConfig();
        EnsureEffectiveDirectoryCreated();
    }

    public void SetCustomTempDirectory(string? path)
    {
        string trimmed = path?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            if (!Directory.Exists(trimmed))
            {
                Directory.CreateDirectory(trimmed);
            }
            _customTempDirectory = Path.GetFullPath(trimmed);
        }
        else
        {
            _customTempDirectory = string.Empty;
        }

        SaveConfig();
        EnsureEffectiveDirectoryCreated();
    }

    public void ResetToDefault()
    {
        _customTempDirectory = string.Empty;
        SaveConfig();
        EnsureEffectiveDirectoryCreated();
    }

    public long GetTempDirectorySize()
    {
        string dir = EffectiveTempDirectory;
        if (!Directory.Exists(dir))
        {
            return 0;
        }

        long totalSize = 0;
        try
        {
            DirectoryInfo di = new(dir);
            foreach (FileInfo fi in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    totalSize += fi.Length;
                }
                catch
                {
                    // Ignore file access errors
                }
            }
        }
        catch
        {
            // Ignore directory enumeration errors
        }

        return totalSize;
    }

    public (int deletedFiles, int deletedDirs, long freedBytes) ClearTempDirectory()
    {
        string dir = EffectiveTempDirectory;
        if (!Directory.Exists(dir))
        {
            return (0, 0, 0);
        }

        int deletedFiles = 0;
        int deletedDirs = 0;
        long freedBytes = 0;

        DirectoryInfo di = new(dir);

        foreach (FileInfo fi in di.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                long length = fi.Length;
                fi.Delete();
                deletedFiles++;
                freedBytes += length;
            }
            catch
            {
                // File locked or in use
            }
        }

        foreach (DirectoryInfo subDi in di.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                long subSize = GetDirectorySize(subDi);
                subDi.Delete(true);
                deletedDirs++;
                freedBytes += subSize;
            }
            catch
            {
                // Subdirectory locked or in use
            }
        }

        return (deletedFiles, deletedDirs, freedBytes);
    }

    private static long GetDirectorySize(DirectoryInfo dirInfo)
    {
        long size = 0;
        try
        {
            foreach (FileInfo fi in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    size += fi.Length;
                }
                catch
                {
                    // Ignore
                }
            }
        }
        catch
        {
            // Ignore
        }
        return size;
    }

    private void EnsureEffectiveDirectoryCreated()
    {
        try
        {
            string dir = EffectiveTempDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch
        {
            // Ignore
        }
    }

    private void LoadConfig()
    {
        try
        {
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("CustomTempDirectory", out JsonElement elem))
                {
                    string? val = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(val) && Directory.Exists(val))
                    {
                        _customTempDirectory = Path.GetFullPath(val);
                    }
                }
            }
        }
        catch
        {
            // Ignore configuration loading errors
        }
    }

    private void SaveConfig()
    {
        try
        {
            string configPath = GetConfigFilePath();
            string? dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var configObj = new
            {
                CustomTempDirectory = _customTempDirectory
            };

            string json = JsonSerializer.Serialize(configObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch
        {
            // Ignore configuration saving errors
        }
    }

    private static string GetConfigFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TifToKmzMapEditor", _configFileName);
    }
}
