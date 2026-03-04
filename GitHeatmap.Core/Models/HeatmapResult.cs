namespace GitHeatmap.Core.Models;

public sealed record ContributionDay(DateOnly Date, int Count);

public sealed class HeatmapResult
{
    public required Dictionary<DateOnly, int> DailyCounts { get; init; }
    public required Dictionary<DateOnly, HashSet<string>> DailyRepoNames { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }

    public int TotalContributions => DailyCounts.Values.Sum();
}
