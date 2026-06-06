using LVDKMovie.Data;
using LVDKMovie.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace LVDKMovie.Controllers;

public class MovieController : Controller
{
    private readonly IHttpClientFactory _http;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public MovieController(IHttpClientFactory http, AppDbContext db, IMemoryCache cache)
    {
        _http = http;
        _db = db;
        _cache = cache;
    }

    public async Task<IActionResult> Detail(string slug)
    {
        try
        {
            var data = await GetMovieDetail(slug);

            if (data?.Movie == null) return NotFound();

            var userId = GetCurrentUserId();
            ViewBag.IsFavorite = userId != null &&
                _db.FavoriteMovies.Any(f => f.UserId == userId.Value && f.Slug == slug);

            return View(data);
        }
        catch
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> Watch(string slug, string? episode, string? server)
    {
        try
        {
            var data = await GetMovieDetail(slug);

            if (data?.Movie == null) return NotFound();

            var userId = GetCurrentUserId() ?? 0;
            var savedHistory = GetSavedHistory(userId, slug);
            if (savedHistory != null)
            {
                if (string.IsNullOrEmpty(server) && !string.IsNullOrWhiteSpace(savedHistory.ServerName))
                {
                    server = savedHistory.ServerName;
                }

                if (string.IsNullOrEmpty(episode) && !string.IsNullOrWhiteSpace(savedHistory.EpisodeSlug))
                {
                    episode = savedHistory.EpisodeSlug;
                }
            }

            var vm = new WatchViewModel
            {
                Movie = data.Movie,
                Episodes = data.Episodes,
            };

            // Pick server
            var serverGroup = string.IsNullOrEmpty(server)
                ? data.Episodes.FirstOrDefault()
                : data.Episodes.FirstOrDefault(s => s.ServerName == server)
                  ?? data.Episodes.FirstOrDefault();

            vm.CurrentServer = serverGroup?.ServerName ?? "";

            // Pick episode
            var ep = PickEpisode(serverGroup, episode, savedHistory);

            vm.CurrentEpisode = ep?.Name ?? "";
            vm.EmbedUrl = ep?.LinkEmbed ?? "";
            vm.M3u8Url = ep?.LinkM3u8 ?? "";
            vm.SubtitleUrl = GetBestSubtitleUrl(ep);

            // Save history
            if (data.Movie != null)
            {
                var existing = GetSavedHistory(userId, slug);
                if (existing != null)
                {
                    existing.Title = data.Movie.Name;
                    existing.Episode = vm.CurrentEpisode;
                    existing.EpisodeSlug = ep?.Slug ?? "";
                    existing.ServerName = vm.CurrentServer;
                    existing.Thumb = data.Movie.ThumbUrl;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.WatchHistories.Add(new WatchHistory
                    {
                        UserId = userId,
                        Slug = slug,
                        Title = data.Movie.Name,
                        Episode = vm.CurrentEpisode,
                        EpisodeSlug = ep?.Slug ?? "",
                        ServerName = vm.CurrentServer,
                        Thumb = data.Movie.ThumbUrl,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                await _db.SaveChangesAsync();
            }

            return View(vm);
        }
        catch
        {
            return NotFound();
        }
    }

    public IActionResult Resume(string slug)
    {
        var userId = GetCurrentUserId() ?? 0;
        var history = GetSavedHistory(userId, slug);
        if (history == null)
        {
            return RedirectToAction("Watch", new { slug });
        }

        if (string.IsNullOrWhiteSpace(history.EpisodeSlug))
        {
            return RedirectToAction("Watch", new { slug, server = history.ServerName });
        }

        return RedirectToAction("Watch", new
        {
            slug,
            episode = history.EpisodeSlug,
            server = history.ServerName
        });
    }

    [HttpPost]
    public IActionResult ToggleFavorite(string slug, string title, string thumb, string poster, int year)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var existing = _db.FavoriteMovies.FirstOrDefault(f => f.UserId == userId.Value && f.Slug == slug);
        if (existing != null)
        {
            _db.FavoriteMovies.Remove(existing);
        }
        else
        {
            _db.FavoriteMovies.Add(new FavoriteMovie
            {
                UserId = userId.Value,
                Slug = slug,
                Title = title,
                Thumb = thumb,
                Poster = poster,
                Year = year,
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.SaveChanges();
        return RedirectToAction("Detail", new { slug });
    }

    [HttpPost]
    public IActionResult DeleteHistory(int id)
    {
        var userId = GetCurrentUserId() ?? 0;
        var item = _db.WatchHistories.FirstOrDefault(h => h.Id == id && h.UserId == userId);
        if (item != null)
        {
            _db.WatchHistories.Remove(item);
            _db.SaveChanges();
        }
        return RedirectToAction("Index", "Home");
    }

    private int? GetCurrentUserId()
    {
        var sessionUserId = HttpContext.Session.GetInt32("UserId");
        if (sessionUserId != null) return sessionUserId;

        if (!Request.Cookies.TryGetValue("LVDKMovie.UserId", out var userIdValue) ||
            !int.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        var user = _db.AppUsers.Find(userId);
        if (user == null) return null;

        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("UserName", user.UserName);
        HttpContext.Session.SetString("DisplayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName);
        HttpContext.Session.SetString("AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? "/images/avatars/default-admin.jpg" : user.AvatarUrl);
        return user.Id;
    }

    private WatchHistory? GetSavedHistory(int userId, string slug)
    {
        return _db.WatchHistories
            .Where(h => h.Slug == slug && (h.UserId == userId || h.UserId == 0))
            .OrderByDescending(h => h.UserId == userId)
            .ThenByDescending(h => h.UpdatedAt)
            .FirstOrDefault();
    }

    private static EpisodeItem? PickEpisode(ServerGroup? serverGroup, string? episode, WatchHistory? savedHistory)
    {
        if (serverGroup == null) return null;

        if (!string.IsNullOrWhiteSpace(episode))
        {
            return serverGroup.ServerData.FirstOrDefault(e => SlugEquals(e.Slug, episode)) ??
                   serverGroup.ServerData.FirstOrDefault(e => NameEquals(e.Name, episode)) ??
                   serverGroup.ServerData.FirstOrDefault();
        }

        if (savedHistory != null)
        {
            return serverGroup.ServerData.FirstOrDefault(e => SlugEquals(e.Slug, savedHistory.EpisodeSlug)) ??
                   serverGroup.ServerData.FirstOrDefault(e => NameEquals(e.Name, savedHistory.Episode)) ??
                   serverGroup.ServerData.FirstOrDefault();
        }

        return serverGroup.ServerData.FirstOrDefault();
    }

    private static bool SlugEquals(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               left.Equals(right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool NameEquals(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               left.Trim().Equals(right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBestSubtitleUrl(EpisodeItem? episode)
    {
        if (episode == null) return "";

        if (!string.IsNullOrWhiteSpace(episode.SubtitleUrl)) return episode.SubtitleUrl;
        if (!string.IsNullOrWhiteSpace(episode.LinkSub)) return episode.LinkSub;

        var subtitles = episode.Subtitles
            .Select(sub => new
            {
                Url = FirstNotEmpty(sub.Url, sub.File, sub.Link),
                Name = $"{sub.Lang} {sub.Language} {sub.Label}"
            })
            .Where(sub => !string.IsNullOrWhiteSpace(sub.Url))
            .ToList();

        var viSubtitle = subtitles.FirstOrDefault(sub =>
            sub.Name.Contains("vi", StringComparison.OrdinalIgnoreCase) ||
            sub.Name.Contains("viet", StringComparison.OrdinalIgnoreCase));

        return viSubtitle?.Url ?? subtitles.FirstOrDefault()?.Url ?? "";
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static MovieDetailResponse? ParseMovieDetail(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Format mới của OPhim: { data: { item: { ..., episodes: [...] } } }
        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("item", out var item))
        {
            var movie = JsonSerializer.Deserialize<MovieDetail>(item.GetRawText());
            var episodes = item.TryGetProperty("episodes", out var itemEpisodes)
                ? JsonSerializer.Deserialize<List<ServerGroup>>(itemEpisodes.GetRawText()) ?? new()
                : new List<ServerGroup>();

            return new MovieDetailResponse
            {
                Status = true,
                Movie = movie,
                Episodes = episodes
            };
        }

        // Format cũ: { movie: ..., episodes: [...] }
        return JsonSerializer.Deserialize<MovieDetailResponse>(json);
    }

    private async Task<MovieDetailResponse?> GetMovieDetail(string slug)
    {
        var cacheKey = $"movie-detail:{slug}";
        if (_cache.TryGetValue(cacheKey, out MovieDetailResponse? cachedDetail) && cachedDetail != null)
        {
            return cachedDetail;
        }

        var client = _http.CreateClient("OPhim");
        var response = await client.GetStringAsync($"phim/{slug}");
        var data = ParseMovieDetail(response);
        if (data?.Movie == null) return data;

        await AddKkPhimThuyetMinhServers(data, slug);
        _cache.Set(cacheKey, data, TimeSpan.FromMinutes(10));
        return data;
    }

    private async Task AddKkPhimThuyetMinhServers(MovieDetailResponse data, string slug)
    {
        try
        {
            var client = _http.CreateClient("KKPhim");
            var response = await client.GetStringAsync($"phim/{slug}");
            var kkData = ParseMovieDetail(response);

            if (kkData?.Episodes == null || kkData.Episodes.Count == 0) return;

            var thuyetMinhServers = kkData.Episodes
                .Where(server => IsThuyetMinh(server.ServerName) ||
                                 server.ServerData.Any(ep => IsThuyetMinh(ep.Filename)))
                .Where(server => server.ServerData.Any())
                .ToList();

            foreach (var server in thuyetMinhServers)
            {
                var serverName = server.ServerName.Contains("Thuyết Minh", StringComparison.OrdinalIgnoreCase)
                    ? server.ServerName
                    : $"{server.ServerName} (Thuyết Minh)";

                if (data.Episodes.Any(existing => existing.ServerName.Equals(serverName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                data.Episodes.Add(new ServerGroup
                {
                    ServerName = serverName,
                    ServerData = server.ServerData
                });
            }

            if (data.Movie != null && !string.IsNullOrWhiteSpace(kkData.Movie?.Lang) &&
                kkData.Movie.Lang.Contains("Thuyết", StringComparison.OrdinalIgnoreCase))
            {
                data.Movie.Lang = kkData.Movie.Lang;
            }
        }
        catch
        {
            // Nguồn KKPhim chỉ là server bổ sung. Nếu lỗi, giữ nguyên nguồn OPhim hiện tại.
        }
    }

    private static bool IsThuyetMinh(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Contains("Thuyết", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Thuyet", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Lồng Tiếng", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Long Tieng", StringComparison.OrdinalIgnoreCase));
    }
}
