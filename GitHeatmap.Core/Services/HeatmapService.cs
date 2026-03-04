using GitHeatmap.Core.Models;

namespace GitHeatmap.Core.Services;

public sealed class HeatmapService
{
    private readonly LocalGitContributionCollector _localCollector = new();
    private readonly GitHubContributionCollector _githubCollector = new();
    private readonly Action<string>? _infoLogger;

    public HeatmapService(Action<string>? infoLogger = null)
    {
        _infoLogger = infoLogger;
    }

    public async Task<HeatmapResult> BuildAsync(HeatmapConfig config, CancellationToken cancellationToken = default)
    {
        var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-(config.LookbackDays - 1)));
        var counts = new Dictionary<DateOnly, int>();
        var repoNamesByDay = new Dictionary<DateOnly, HashSet<string>>();
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
                            AddRepoForDay(repoNamesByDay, day, repo.Name);
                        }

                        break;
                    }
                    case RepoType.GitHub:
                    {
                        var dates = await _githubCollector.CollectAsync(repo, since, config.AuthorMatch, _infoLogger, cancellationToken);
                        foreach (var day in dates)
                        {
                            counts[day] = counts.TryGetValue(day, out var current) ? current + 1 : 1;
                            AddRepoForDay(repoNamesByDay, day, repo.Name);
                        }

                        break;
                    }
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
            DailyRepoNames = repoNamesByDay,
            Warnings = warnings
        };
    }

    private static void AddRepoForDay(Dictionary<DateOnly, HashSet<string>> repoNamesByDay, DateOnly day, string repoName)
    {
        if (!repoNamesByDay.TryGetValue(day, out var names))
        {
            names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            repoNamesByDay[day] = names;
        }

        names.Add(string.IsNullOrWhiteSpace(repoName) ? "Unnamed Repo" : repoName.Trim());
    }
}
