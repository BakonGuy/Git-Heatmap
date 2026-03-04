using GitHeatmap.Core.Export;
using GitHeatmap.Core.Services;

var workingDirectory = Directory.GetCurrentDirectory();
var configPath = ConfigPathResolver.ResolveFrom(workingDirectory);
await ConfigLoader.WriteTemplateIfMissingAsync(configPath);

var argsList = args.ToList();
var command = argsList.FirstOrDefault()?.ToLowerInvariant() ?? "refresh";

var config = await ConfigLoader.LoadAsync(configPath);
var service = new HeatmapService(message => Console.WriteLine($"Info: {message}"));
var result = await service.BuildAsync(config);

switch (command)
{
    case "refresh":
        Console.WriteLine($"Total commits: {result.TotalContributions}");
        Console.WriteLine($"Active days: {result.DailyCounts.Count(kvp => kvp.Value > 0)}");
        break;

    case "export":
    {
        var htmlIndex = argsList.FindIndex(x => x.Equals("--html", StringComparison.OrdinalIgnoreCase));
        var pngIndex = argsList.FindIndex(x => x.Equals("--png", StringComparison.OrdinalIgnoreCase));

        var hasHtml = htmlIndex != -1 && htmlIndex < argsList.Count - 1;
        var hasPng = pngIndex != -1 && pngIndex < argsList.Count - 1;

        if (!hasHtml && !hasPng)
        {
            Console.WriteLine("Usage: export --html <path> [--png <path>]");
            return;
        }

        if (hasHtml)
        {
            var outputPath = Path.GetFullPath(argsList[htmlIndex + 1]);
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

            var outputPath = Path.GetFullPath(argsList[pngIndex + 1]);
            CliPngExporter.Export(result, config.LookbackDays, outputPath);
            Console.WriteLine($"PNG exported to {outputPath}");
        }

        break;
    }

    default:
        Console.WriteLine("Commands: refresh | export --html <path> [--png <path>]");
        break;
}

foreach (var warning in result.Warnings)
{
    Console.WriteLine($"Warning: {warning}");
}

Console.WriteLine($"Config: {configPath}");
