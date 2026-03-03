using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitHeatmap.Core.Models;

namespace GitHeatmap.Core.Services;

public sealed class GitHubContributionCollector
{
    private static readonly HttpClient HttpClient = CreateClient();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GitHeatmap",
        "cache",
        "github");

    public async Task<IReadOnlyList<DateOnly>> CollectAsync(
        RepoConfig repository,
        DateOnly since,
        string? authorMatch,
        Action<string>? infoLogger = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repository.Owner) || string.IsNullOrWhiteSpace(repository.Repo))
        {
            throw new InvalidOperationException($"GitHub repo '{repository.Name}' is missing Owner or Repo.");
        }

        var owner = repository.Owner.Trim();
        var repo = repository.Repo.Trim();
        var cacheKey = BuildCacheKey(owner, repo, since, authorMatch);
        var cached = await TryReadCacheAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            infoLogger?.Invoke($"Using cached GitHub data for {owner}/{repo} (valid for 1 hour).");
            return cached;
        }

        var requestUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?since={since:yyyy-MM-dd}T00:00:00Z&per_page=100";
        var results = new List<DateOnly>();

        while (!string.IsNullOrWhiteSpace(requestUrl))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw BuildApiException(response, body, owner, repo);
            }

            using var document = JsonDocument.Parse(body);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!MatchesAuthor(item, authorMatch))
                {
                    continue;
                }

                var dateText = item.GetProperty("commit").GetProperty("author").GetProperty("date").GetString();
                if (DateTime.TryParse(dateText, out var commitDateTime))
                {
                    results.Add(DateOnly.FromDateTime(commitDateTime.Date));
                }
            }

            requestUrl = GetNextPageUrl(response.Headers);
        }

        await WriteCacheAsync(cacheKey, results, cancellationToken);
        return results;
    }

    private static string BuildCacheKey(string owner, string repo, DateOnly since, string? authorMatch)
    {
        var raw = $"{owner}/{repo}|{since:yyyy-MM-dd}|{authorMatch?.Trim() ?? string.Empty}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetCachePath(string cacheKey) => Path.Combine(CacheDirectory, $"{cacheKey}.json");

    private static async Task<IReadOnlyList<DateOnly>?> TryReadCacheAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var path = GetCachePath(cacheKey);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var cached = await JsonSerializer.DeserializeAsync<CachedGitHubResult>(stream, cancellationToken: cancellationToken);
            if (cached is null)
            {
                return null;
            }

            if (DateTimeOffset.UtcNow - cached.CreatedUtc > CacheTtl)
            {
                return null;
            }

            return cached.DayNumbers.Select(DateOnly.FromDayNumber).ToList();
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteCacheAsync(string cacheKey, IReadOnlyList<DateOnly> dates, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            var payload = new CachedGitHubResult
            {
                CreatedUtc = DateTimeOffset.UtcNow,
                DayNumbers = dates.Select(x => x.DayNumber).ToList()
            };

            var path = GetCachePath(cacheKey);
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, payload, cancellationToken: cancellationToken);
        }
        catch
        {
            // Cache write failures should not break data collection.
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GitHeatmapApp/1.0");
        return client;
    }

    private static Exception BuildApiException(HttpResponseMessage response, string body, string owner, string repo)
    {
        var code = (int)response.StatusCode;
        var reason = response.ReasonPhrase ?? "GitHub API request failed";
        var message = $"GitHub API error for {owner}/{repo}: {code} {reason}.";

        if (response.StatusCode == HttpStatusCode.Forbidden &&
            response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues) &&
            remainingValues.FirstOrDefault() == "0")
        {
            var resetText = response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues)
                ? resetValues.FirstOrDefault()
                : null;
            if (long.TryParse(resetText, out var resetUnix))
            {
                var reset = DateTimeOffset.FromUnixTimeSeconds(resetUnix).LocalDateTime;
                message += $" Rate limit exceeded. Resets at {reset:yyyy-MM-dd HH:mm:ss}.";
            }
            else
            {
                message += " Rate limit exceeded.";
            }
        }

        var apiMessage = TryExtractApiMessage(body);
        if (!string.IsNullOrWhiteSpace(apiMessage))
        {
            message += $" Details: {apiMessage}";
        }

        return new InvalidOperationException(message);
    }

    private static string? TryExtractApiMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }
        }
        catch
        {
            // Ignore parse failures and return null.
        }

        return null;
    }

    private static bool MatchesAuthor(JsonElement commitItem, string? authorMatch)
    {
        if (string.IsNullOrWhiteSpace(authorMatch))
        {
            return true;
        }

        var match = authorMatch.Trim();

        var authorLogin = commitItem.TryGetProperty("author", out var authorElement) &&
                          authorElement.ValueKind == JsonValueKind.Object &&
                          authorElement.TryGetProperty("login", out var loginElement)
            ? loginElement.GetString()
            : null;

        var commitAuthor = commitItem.GetProperty("commit").GetProperty("author");
        var authorName = commitAuthor.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var authorEmail = commitAuthor.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;

        return ContainsIgnoreCase(authorLogin, match) ||
               ContainsIgnoreCase(authorName, match) ||
               ContainsIgnoreCase(authorEmail, match);
    }

    private static bool ContainsIgnoreCase(string? source, string value)
    {
        return source?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? GetNextPageUrl(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Link", out var linkValues))
        {
            return null;
        }

        var linkHeader = linkValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(linkHeader))
        {
            return null;
        }

        foreach (var segment in linkHeader.Split(','))
        {
            var parts = segment.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var urlPart = parts[0];
            var relPart = parts[1];
            if (!relPart.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (urlPart.StartsWith('<') && urlPart.EndsWith('>'))
            {
                return urlPart[1..^1];
            }
        }

        return null;
    }

    private sealed class CachedGitHubResult
    {
        public DateTimeOffset CreatedUtc { get; set; }
        public List<int> DayNumbers { get; set; } = [];
    }
}
