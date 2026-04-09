namespace GitHeatmap.Core.Services;

public static class ConfigPathResolver
{
    public const string ConfigFileName = "heatmap.config.json";
    private const string AppFolderName = "GitHeatmap";
    public const string OutputFolderName = "Output";
    public const string DefaultPngFileName = "Heatmap.png";

    public static string ResolveFrom(string startDirectory)
    {
        var appDataPath = GetAppDataConfigPath();
        if (File.Exists(appDataPath))
        {
            return appDataPath;
        }

        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ConfigFileName);
            if (File.Exists(candidate))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(appDataPath)!);
                File.Copy(candidate, appDataPath, overwrite: false);
                return appDataPath;
            }

            current = current.Parent;
        }

        return appDataPath;
    }

    private static string GetAppDataConfigPath()
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseFolder, AppFolderName, ConfigFileName);
    }

    public static string GetDefaultPngOutputPath()
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseFolder, AppFolderName, OutputFolderName, DefaultPngFileName);
    }
}
