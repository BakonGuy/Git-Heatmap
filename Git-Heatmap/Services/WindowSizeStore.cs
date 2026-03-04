using System.Text.Json;
using System.Windows;
using System.IO;

namespace Git_Heatmap.Services;

public static class WindowSizeStore
{
    private const double MinWidth = 320;
    private const double MinHeight = 240;
    private const double MinLeft = -10000;
    private const double MaxLeft = 10000;
    private const double MinTop = -10000;
    private const double MaxTop = 10000;

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

        if (size.Left >= MinLeft && size.Left <= MaxLeft)
        {
            window.Left = size.Left;
        }

        if (size.Top >= MinTop && size.Top <= MaxTop)
        {
            window.Top = size.Top;
        }
    }

    public static bool HasSavedState(string key)
    {
        var map = Load();
        return map.ContainsKey(key);
    }

    public static void Save(Window window, string key)
    {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        var source = bounds.Size;
        if (source.Width < MinWidth || source.Height < MinHeight)
        {
            return;
        }

        var map = Load();
        map[key] = new SavedWindowSize(source.Width, source.Height, bounds.Left, bounds.Top);

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

    private sealed record SavedWindowSize(double Width, double Height, double Left, double Top);
}
