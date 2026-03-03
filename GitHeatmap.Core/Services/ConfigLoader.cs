using System.Text.Json;
using GitHeatmap.Core.Models;

namespace GitHeatmap.Core.Services;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<HeatmapConfig> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<HeatmapConfig>(stream, JsonOptions, cancellationToken);
        return config ?? new HeatmapConfig();
    }

    public static async Task SaveAsync(string path, HeatmapConfig config, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public static async Task WriteTemplateIfMissingAsync(string path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
        {
            return;
        }

        var template = new HeatmapConfig
        {
            LookbackDays = 365,
            AuthorMatch = null,
            Repositories =
            [
                new RepoConfig { Name = "ExampleLocalRepo", Type = RepoType.Local, Path = "C:/src/my-repo" },
                new RepoConfig { Name = "ExampleRemoteRepo", Type = RepoType.GitHub, Owner = "octocat", Repo = "Hello-World" }
            ]
        };

        var json = JsonSerializer.Serialize(template, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }
}
