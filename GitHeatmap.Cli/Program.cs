using GitHeatmap.Core.Export;
using GitHeatmap.Core.Services;

var workingDirectory = Directory.GetCurrentDirectory();
var configPath = ConfigPathResolver.ResolveFrom(workingDirectory);
await ConfigLoader.WriteTemplateIfMissingAsync(configPath);

var argsList = args.ToList();
var command = argsList.FirstOrDefault()?.ToLowerInvariant() ?? "refresh";

var config = await ConfigLoader.LoadAsync(configPath);
var service = new HeatmapService();
var result = await service.BuildAsync(config);

switch( command )
{
	case "refresh":
		Console.WriteLine($"Total contributions: {result.TotalContributions}");
		Console.WriteLine($"Active days: {result.DailyCounts.Count(kvp => kvp.Value > 0)}");
		break;

	case "export":
	{
		var htmlIndex = argsList.FindIndex(x => x.Equals("--html", StringComparison.OrdinalIgnoreCase));
		if( htmlIndex == -1 || htmlIndex == argsList.Count - 1 )
		{
			Console.WriteLine("Usage: export --html <path>");
			return;
		}

		var outputPath = Path.GetFullPath(argsList[htmlIndex + 1]);
		var html = HtmlHeatmapExporter.Export(result, config.LookbackDays);
		await File.WriteAllTextAsync(outputPath, html);
		Console.WriteLine($"HTML exported to {outputPath}");
		break;
	}

	default:
		Console.WriteLine("Commands: refresh | export --html <path>");
		break;
}

foreach( var warning in result.Warnings )
{
	Console.WriteLine($"Warning: {warning}");
}

Console.WriteLine($"Config: {configPath}");