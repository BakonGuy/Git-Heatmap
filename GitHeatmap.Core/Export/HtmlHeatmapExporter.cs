using System.Text;
using GitHeatmap.Core.Models;

namespace GitHeatmap.Core.Export;

public static class HtmlHeatmapExporter
{
    private static readonly string[] Palette = ["#161b22", "#0e4429", "#006d32", "#26a641", "#39d353"];

    public static string Export(HeatmapResult result, int lookbackDays, DateOnly? endDate = null)
    {
        var end = endDate ?? DateOnly.FromDateTime(DateTime.Today);
        var start = end.AddDays(-(lookbackDays - 1));

        var max = result.DailyCounts.Values.DefaultIfEmpty(0).Max();

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Git Heatmap</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;background:#0d1117;color:#c9d1d9;padding:24px}.grid{display:grid;grid-template-columns:repeat(53,12px);grid-template-rows:repeat(7,12px);gap:3px}.cell{width:12px;height:12px;border-radius:2px}.meta{margin-bottom:10px}</style></head><body>");
        sb.AppendLine($"<div class='meta'><strong>{result.TotalContributions}</strong> contributions in the last {lookbackDays} days</div>");
        sb.AppendLine("<div class='grid'>");

        var startOnSunday = start.AddDays(-(int)start.DayOfWeek);
        for (var week = 0; week < 53; week++)
        {
            for (var day = 0; day < 7; day++)
            {
                var current = startOnSunday.AddDays((week * 7) + day);
                var count = result.DailyCounts.TryGetValue(current, out var c) && current >= start && current <= end ? c : 0;
                var level = ToLevel(count, max);
                var color = Palette[level];
                sb.AppendLine($"<div class='cell' title='{current:yyyy-MM-dd}: {count}' style='background:{color}'></div>");
            }
        }

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    public static int ToLevel(int count, int maxCount)
    {
        if (count <= 0 || maxCount <= 0) return 0;

        var ratio = (double)count / maxCount;
        if (ratio <= 0.25) return 1;
        if (ratio <= 0.5) return 2;
        if (ratio <= 0.75) return 3;
        return 4;
    }
}
