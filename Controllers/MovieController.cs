using LVDKMovie.Data;
using LVDKMovie.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LVDKMovie.Controllers;

public class MovieController : Controller
{
    private readonly IHttpClientFactory _http;
    private readonly AppDbContext _db;

    public MovieController(IHttpClientFactory http, AppDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task<IActionResult> Detail(string slug)
    {
        var client = _http.CreateClient("OPhim");

        try
        {
            var resp = await client.GetStringAsync($"phim/{slug}");
            var data = ParseMovieDetail(resp);

            if (data?.Movie == null) return NotFound();
            await AddKkPhimThuyetMinhServers(data, slug);

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
        var client = _http.CreateClient("OPhim");

        try
        {
            var resp = await client.GetStringAsync($"phim/{slug}");
            var data = ParseMovieDetail(resp);

            if (data?.Movie == null) return NotFound();
            await AddKkPhimThuyetMinhServers(data, slug);

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
            var ep = string.IsNullOrEmpty(episode)
                ? serverGroup?.ServerData.FirstOrDefault()
                : serverGroup?.ServerData.FirstOrDefault(e => e.Slug == episode)
                  ?? serverGroup?.ServerData.FirstOrDefault();

            vm.CurrentEpisode = ep?.Name ?? "";
            vm.EmbedUrl = ep?.LinkEmbed ?? "";
            vm.M3u8Url = ep?.LinkM3u8 ?? "";
            vm.SubtitleUrl = GetBestSubtitleUrl(ep);

            // Save history
            if (data.Movie != null)
            {
                var existing = _db.WatchHistories.FirstOrDefault(h => h.Slug == slug);
                if (existing != null)
                {
                    existing.Episode = vm.CurrentEpisode;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.WatchHistories.Add(new WatchHistory
                    {
                        Slug = slug,
                        Title = data.Movie.Name,
                        Episode = vm.CurrentEpisode,
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
        var item = _db.WatchHistories.Find(id);
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

    private async Task AddKkPhimThuyetMinhServers(MovieDetailResponse data, string slug)
    {
        try
        {
            var client = _http.CreateClient("KKPhim");
            var response = await client.GetStringAsync($"phim/{slug}");
            var kkData = JsonSerializer.Deserialize<MovieDetailResponse>(response);

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
