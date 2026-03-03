namespace GitHeatmap.Core.Services;

public static class ConfigPathResolver
{
    public const string ConfigFileName = "heatmap.config.json";

    public static string ResolveFrom(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ConfigFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(startDirectory, ConfigFileName);
    }
}
