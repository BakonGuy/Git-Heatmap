using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;
using GitHeatmap.Core.Export;
using GitHeatmap.Core.Models;

namespace Git_Heatmap;

[SupportedOSPlatform("windows6.1")]
public static class CliPngExporter
{
    private static readonly Color[] Palette =
    [
        ColorTranslator.FromHtml("#1a1a1a"),
        ColorTranslator.FromHtml("#4a2a0a"),
        ColorTranslator.FromHtml("#7a3e0b"),
        ColorTranslator.FromHtml("#b45309"),
        ColorTranslator.FromHtml("#f97316")
    ];

    public static void Export(HeatmapResult result, int lookbackDays, string outputPath)
    {
        const int cellSize = 12;
        const int cellGap = 3;
        const int weeks = 53;
        const int rows = 7;
        const int padding = 24;
        const int summaryHeight = 32;
        const int legendHeight = 24;

        var gridWidth = (weeks * cellSize) + ((weeks - 1) * cellGap);
        var gridHeight = (rows * cellSize) + ((rows - 1) * cellGap);
        var width = padding * 2 + gridWidth;
        var height = padding + summaryHeight + gridHeight + legendHeight + padding;

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(ColorTranslator.FromHtml("#111111"));
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var summaryBrush = new SolidBrush(ColorTranslator.FromHtml("#e6e6e6"));
        using var summaryFont = new Font("Segoe UI", 16, FontStyle.Bold);
        graphics.DrawString($"{result.TotalContributions} commits in the last {lookbackDays} days", summaryFont, summaryBrush, padding, padding - 1);

        var end = DateOnly.FromDateTime(DateTime.Today);
        var start = end.AddDays(-(lookbackDays - 1));
        var startOnSunday = start.AddDays(-(int)start.DayOfWeek);
        var max = result.DailyCounts.Values.DefaultIfEmpty(0).Max();

        var gridY = padding + summaryHeight;
        for (var week = 0; week < weeks; week++)
        {
            for (var day = 0; day < rows; day++)
            {
                var current = startOnSunday.AddDays((week * 7) + day);
                if (current > end)
                {
                    continue;
                }

                var count = result.DailyCounts.TryGetValue(current, out var c) && current >= start && current <= end ? c : 0;
                var level = HtmlHeatmapExporter.ToLevel(count, max);
                var color = Palette[level];

                var x = padding + week * (cellSize + cellGap);
                var y = gridY + day * (cellSize + cellGap);
                using var brush = new SolidBrush(color);
                graphics.FillRectangle(brush, x, y, cellSize, cellSize);
            }
        }

        DrawLegend(graphics, width, padding, gridY + gridHeight + 12, cellSize, cellGap);
        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private static void DrawLegend(Graphics graphics, int imageWidth, int padding, int topY, int cellSize, int cellGap)
    {
        using var textBrush = new SolidBrush(ColorTranslator.FromHtml("#9a9a9a"));
        using var labelFont = new Font("Segoe UI", 12, FontStyle.Regular);

        var lessText = "Less";
        var moreText = "More";
        var lessSize = graphics.MeasureString(lessText, labelFont);
        var moreSize = graphics.MeasureString(moreText, labelFont);

        const int boxCount = 5;
        var boxesWidth = (boxCount * cellSize) + ((boxCount - 1) * cellGap);
        const int spacing = 8;
        var totalWidth = (int)Math.Ceiling(lessSize.Width + spacing + boxesWidth + spacing + moreSize.Width);
        var startX = imageWidth - padding - totalWidth;

        var targetMidY = topY + 2 + (cellSize / 2f);
        var lessY = targetMidY - (lessSize.Height / 2f);
        graphics.DrawString(lessText, labelFont, textBrush, startX, lessY);

        var boxesStartX = startX + (int)Math.Ceiling(lessSize.Width) + spacing;
        for (var i = 0; i < boxCount; i++)
        {
            using var brush = new SolidBrush(Palette[i]);
            var x = boxesStartX + i * (cellSize + cellGap);
            graphics.FillRectangle(brush, x, topY + 2, cellSize, cellSize);
        }

        var moreX = boxesStartX + boxesWidth + spacing;
        var moreY = targetMidY - (moreSize.Height / 2f);
        graphics.DrawString(moreText, labelFont, textBrush, moreX, moreY);
    }
}
