using System.Text.Json.Serialization;

namespace GitHeatmap.Core.Models;

public sealed class HeatmapConfig
{
    public int LookbackDays { get; set; } = 365;
    public string? AuthorMatch { get; set; }
    public List<RepoConfig> Repositories { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RepoType
{
    Local,
    GitHub
}

public sealed class RepoConfig
{
    public string Name { get; set; } = string.Empty;
    public RepoType Type { get; set; } = RepoType.Local;
    public string? Path { get; set; }
    public string? Owner { get; set; }
    public string? Repo { get; set; }
}
