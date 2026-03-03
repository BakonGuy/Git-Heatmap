using GitHeatmap.Core.Models;

namespace GitHeatmap.Core.Services;

public sealed class HeatmapService
{
    private readonly LocalGitContributionCollector _localCollector = new();

    public async Task<HeatmapResult> BuildAsync(HeatmapConfig config, CancellationToken cancellationToken = default)
    {
        var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-(config.LookbackDays - 1)));
        var counts = new Dictionary<DateOnly, int>();
        var warnings = new List<string>();

        foreach (var repo in config.Repositories)
        {
            try
            {
                switch (repo.Type)
                {
                    case RepoType.Local:
                    {
                        var dates = await _localCollector.CollectAsync(repo, since, config.AuthorMatch, cancellationToken);
                        foreach (var day in dates)
                        {
                            counts[day] = counts.TryGetValue(day, out var current) ? current + 1 : 1;
                        }

                        break;
                    }
                    case RepoType.GitHub:
                        warnings.Add($"Skipping '{repo.Name}': remote GitHub collection is not implemented yet.");
                        break;
                    default:
                        warnings.Add($"Skipping '{repo.Name}': unsupported repository type '{repo.Type}'.");
                        break;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to read '{repo.Name}': {ex.Message}");
            }
        }

        return new HeatmapResult
        {
            DailyCounts = counts,
            Warnings = warnings
        };
    }
}
