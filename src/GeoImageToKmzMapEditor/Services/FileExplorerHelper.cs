using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TifToKmzMapEditor.Services;

public static class FileExplorerHelper
{
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string folderToOpen = path;

        if (File.Exists(path))
        {
            folderToOpen = Path.GetDirectoryName(path) ?? path;
        }

        if (!Directory.Exists(folderToOpen))
        {
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folderToOpen}\"",
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{folderToOpen}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{folderToOpen}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore explorer launch errors
        }
    }
}
