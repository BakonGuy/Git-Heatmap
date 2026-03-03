using System.Text.Json;
using System.Windows;
using System.IO;

namespace Git_Heatmap.Services;

public static class WindowSizeStore
{
    private const double MinWidth = 320;
    private const double MinHeight = 240;

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GitHeatmap",
        "window-sizes.json");

    public static void Apply(Window window, string key)
    {
        var map = Load();
        if (!map.TryGetValue(key, out var size))
        {
            return;
        }

        if (size.Width >= MinWidth)
        {
            window.Width = size.Width;
        }

        if (size.Height >= MinHeight)
        {
            window.Height = size.Height;
        }
    }

    public static void Save(Window window, string key)
    {
        var source = window.WindowState == WindowState.Normal ? new Size(window.Width, window.Height) : window.RestoreBounds.Size;
        if (source.Width < MinWidth || source.Height < MinHeight)
        {
            return;
        }

        var map = Load();
        map[key] = new SavedWindowSize(source.Width, source.Height);

        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StorePath, json);
    }

    private static Dictionary<string, SavedWindowSize> Load()
    {
        if (!File.Exists(StorePath))
        {
            return new Dictionary<string, SavedWindowSize>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<Dictionary<string, SavedWindowSize>>(json)
                   ?? new Dictionary<string, SavedWindowSize>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, SavedWindowSize>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed record SavedWindowSize(double Width, double Height);
}
