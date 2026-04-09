using System.IO;
using System.Runtime.InteropServices;
using GitHeatmap.Core.Export;
using GitHeatmap.Core.Models;
using GitHeatmap.Core.Services;

namespace Git_Heatmap;

internal static class CommandLineHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        TryAttachToParentConsole();

        var parsed = Parse(args);
        if (!parsed.Success)
        {
            WriteError(parsed.ErrorMessage ?? "Invalid command.");
            PrintUsage();
            return 1;
        }

        var workingDirectory = Directory.GetCurrentDirectory();
        var configPath = parsed.ConfigPath is null
            ? ConfigPathResolver.ResolveFrom(workingDirectory)
            : Path.GetFullPath(parsed.ConfigPath);

        await ConfigLoader.WriteTemplateIfMissingAsync(configPath);
        HeatmapConfig config;
        HeatmapResult result;
        try
        {
            config = await ConfigLoader.LoadAsync(configPath);
            var service = new HeatmapService(message => Console.WriteLine($"Info: {message}"));
            result = await service.BuildAsync(config);
        }
        catch (Exception ex)
        {
            WriteError($"Failed to build heatmap data: {ex.Message}");
            return 1;
        }

        switch (parsed.Command)
        {
            case "refresh":
                Console.WriteLine($"Total commits: {result.TotalContributions}");
                Console.WriteLine($"Active days: {result.DailyCounts.Count(kvp => kvp.Value > 0)}");
                break;
            case "export":
                try
                {
                    if (parsed.HtmlPath is not null)
                    {
                        var htmlOutputPath = Path.GetFullPath(parsed.HtmlPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(htmlOutputPath) ?? ".");
                        var html = HtmlHeatmapExporter.Export(result, config.LookbackDays);
                        await File.WriteAllTextAsync(htmlOutputPath, html);
                        Console.WriteLine($"HTML exported to {htmlOutputPath}");
                    }

                    if (parsed.PngPath is not null)
                    {
                        var pngOutputPath = Path.GetFullPath(parsed.PngPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(pngOutputPath) ?? ".");
                        CliPngExporter.Export(result, config.LookbackDays, pngOutputPath);
                        Console.WriteLine($"PNG exported to {pngOutputPath}");
                    }
                }
                catch (Exception ex)
                {
                    WriteError($"Failed to export PNG: {ex.Message}");
                    return 1;
                }

                break;
            default:
                WriteError("Unsupported command.");
                PrintUsage();
                return 1;
        }

        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        Console.WriteLine($"Config: {configPath}");
        return 0;
    }

    private static ParsedArgs Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return ParsedArgs.Fail("No command provided.");
        }

        var command = args[0].ToLowerInvariant();
        if (command is not ("refresh" or "export"))
        {
            return ParsedArgs.Fail($"Unknown command '{args[0]}'.");
        }

        var parsed = new ParsedArgs { Success = true, Command = command };
        for (var i = 1; i < args.Count; i++)
        {
            var token = args[i];
            switch (token.ToLowerInvariant())
            {
                case "--config":
                    if (!TryReadValue(args, ref i, out var configValue))
                    {
                        return ParsedArgs.Fail("Missing value for --config.");
                    }

                    parsed.ConfigPath = configValue;
                    break;
                case "--html":
                    if (!TryReadValue(args, ref i, out var htmlValue))
                    {
                        return ParsedArgs.Fail("Missing value for --html.");
                    }

                    parsed.HtmlPath = htmlValue;
                    break;
                case "--png":
                    if (TryReadValue(args, ref i, out var pngValue))
                    {
                        parsed.PngPath = pngValue;
                    }
                    else
                    {
                        parsed.PngPath = ConfigPathResolver.GetDefaultPngOutputPath();
                    }

                    break;
                default:
                    return ParsedArgs.Fail($"Unknown argument '{token}'.");
            }
        }

        if (command == "refresh")
        {
            if (parsed.HtmlPath is not null || parsed.PngPath is not null)
            {
                return ParsedArgs.Fail("refresh does not support --html or --png.");
            }

            return parsed;
        }

        if (parsed.HtmlPath is null && parsed.PngPath is null)
        {
            return ParsedArgs.Fail("export requires --html <path> and/or --png [path].");
        }

        return parsed;
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string? value)
    {
        var next = index + 1;
        if (next >= args.Count || args[next].StartsWith("--", StringComparison.Ordinal))
        {
            value = null;
            return false;
        }

        value = args[next];
        index = next;
        return true;
    }

    private static void WriteError(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  Git-Heatmap.exe");
        Console.WriteLine("  Git-Heatmap.exe refresh [--config <path>]");
        Console.WriteLine("  Git-Heatmap.exe export [--config <path>] [--html <path>] [--png [path]]");
    }

    private static void TryAttachToParentConsole()
    {
        const uint attachParentProcess = 0xFFFFFFFF;
        AttachConsole(attachParentProcess);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    private sealed class ParsedArgs
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string Command { get; init; } = "refresh";
        public string? ConfigPath { get; set; }
        public string? HtmlPath { get; set; }
        public string? PngPath { get; set; }

        public static ParsedArgs Fail(string message) => new() { Success = false, ErrorMessage = message };
    }
}
