using GitHeatmap.Core.Export;
using GitHeatmap.Core.Services;

var workingDirectory = Directory.GetCurrentDirectory();
var configPath = ConfigPathResolver.ResolveFrom(workingDirectory);
await ConfigLoader.WriteTemplateIfMissingAsync(configPath);

var argsList = args.ToList();
var command = argsList.FirstOrDefault()?.ToLowerInvariant() ?? "refresh";

GitHeatmap.Core.Models.HeatmapConfig config;
GitHeatmap.Core.Models.HeatmapResult result;
try
{
    config = await ConfigLoader.LoadAsync(configPath);
    var service = new HeatmapService(message => Console.WriteLine($"Info: {message}"));
    result = await service.BuildAsync(config);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: failed to build heatmap data: {ex.Message}");
    return;
}

switch (command)
{
    case "refresh":
        if (argsList.Count > 1)
        {
            Console.WriteLine("Error: refresh does not accept additional arguments.");
            return;
        }

        Console.WriteLine($"Total commits: {result.TotalContributions}");
        Console.WriteLine($"Active days: {result.DailyCounts.Count(kvp => kvp.Value > 0)}");
        break;

    case "export":
    {
        var htmlIndex = argsList.FindIndex(x => x.Equals("--html", StringComparison.OrdinalIgnoreCase));
        var pngIndex = argsList.FindIndex(x => x.Equals("--png", StringComparison.OrdinalIgnoreCase));

        var hasHtml = htmlIndex != -1 && htmlIndex < argsList.Count - 1 && !argsList[htmlIndex + 1].StartsWith("--", StringComparison.Ordinal);
        var hasPng = pngIndex != -1;

        if (!hasHtml && !hasPng)
        {
            Console.WriteLine("Error: export requires --html <path> and/or --png [path].");
            Console.WriteLine("Usage: export --html <path> [--png [path]]");
            return;
        }

        if (hasHtml)
        {
            var outputPath = Path.GetFullPath(argsList[htmlIndex + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            var html = HtmlHeatmapExporter.Export(result, config.LookbackDays);
            await File.WriteAllTextAsync(outputPath, html);
            Console.WriteLine($"HTML exported to {outputPath}");
        }

        if (hasPng)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                Console.WriteLine("PNG export from CLI is only supported on Windows.");
                break;
            }

            var outputPath = pngIndex < argsList.Count - 1 && !argsList[pngIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? Path.GetFullPath(argsList[pngIndex + 1])
                : ConfigPathResolver.GetDefaultPngOutputPath();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            try
            {
                CliPngExporter.Export(result, config.LookbackDays, outputPath);
                Console.WriteLine($"PNG exported to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: failed to export PNG: {ex.Message}");
                return;
            }
        }

        break;
    }

    default:
        Console.WriteLine($"Error: unknown command '{command}'.");
        Console.WriteLine("Commands: refresh | export --html <path> [--png [path]]");
        return;
}

foreach (var warning in result.Warnings)
{
    Console.WriteLine($"Warning: {warning}");
}

Console.WriteLine($"Config: {configPath}");
