using System.Diagnostics;
using GitHeatmap.Core.Models;

namespace GitHeatmap.Core.Services;

public sealed class LocalGitContributionCollector
{
    public async Task<IReadOnlyList<DateOnly>> CollectAsync(RepoConfig repository, DateOnly since, string? authorMatch, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repository.Path) || !Directory.Exists(repository.Path))
        {
            throw new DirectoryNotFoundException($"Local repo path not found for '{repository.Name}'.");
        }

        var arguments = $"log --date=short --pretty=format:%ad --since=\"{since:yyyy-MM-dd}\"";
        if (!string.IsNullOrWhiteSpace(authorMatch))
        {
            arguments += $" --author=\"{authorMatch}\"";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repository.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git log failed for '{repository.Name}': {error}");
        }

        var dates = new List<DateOnly>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (DateOnly.TryParse(line, out var date))
            {
                dates.Add(date);
            }
        }

        return dates;
    }
}
