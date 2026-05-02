namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Detects the Kindle "My Clippings.txt" file path across macOS, Linux, and Windows.
/// </summary>
public static class KindleDetector
{
    private const string ClippingsRelativePath = "documents/My Clippings.txt";

    public static string? DetectClippingsPath()
    {
        if (OperatingSystem.IsMacOS())
            return ProbeMacOS();

        if (OperatingSystem.IsLinux())
            return ProbeLinux();

        if (OperatingSystem.IsWindows())
            return ProbeWindows();

        return null;
    }

    private static string? ProbeMacOS()
    {
        var path = Path.Combine("/Volumes/Kindle", ClippingsRelativePath);
        return File.Exists(path) ? path : null;
    }

    private static string? ProbeLinux()
    {
        // Try /media/<user>/Kindle and /run/media/<user>/Kindle
        foreach (var baseDir in new[] { "/media", "/run/media" })
        {
            if (!Directory.Exists(baseDir))
                continue;

            try
            {
                foreach (var userDir in Directory.EnumerateDirectories(baseDir))
                {
                    var path = Path.Combine(userDir, "Kindle", ClippingsRelativePath);
                    if (File.Exists(path))
                        return path;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't read
            }
        }

        return null;
    }

    private static string? ProbeWindows()
    {
        // Check drives D through G
        foreach (var drive in new[] { 'D', 'E', 'F', 'G' })
        {
            var path = Path.Combine($"{drive}:\\", ClippingsRelativePath);
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
