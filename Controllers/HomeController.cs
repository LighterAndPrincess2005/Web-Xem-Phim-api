using LVDKMovie.Data;
using LVDKMovie.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace LVDKMovie.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _http;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public HomeController(IHttpClientFactory http, AppDbContext db, IMemoryCache cache)
    {
        _http = http;
        _db = db;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var client = _http.CreateClient("OPhim");
        var latestTask = GetMovieItems(client, "danh-sach/phim-moi-cap-nhat?page=1");
        var seriesTask = GetMovieItems(client, "danh-sach/phim-bo?page=1");
        var singleTask = GetMovieItems(client, "danh-sach/phim-le?page=1");
        var koreanTask = GetMovieItems(client, "quoc-gia/han-quoc?page=1");
        var chineseTask = GetMovieItems(client, "quoc-gia/trung-quoc?page=1");

        await Task.WhenAll(latestTask, seriesTask, singleTask, koreanTask, chineseTask);

        var items = KeepRecent(latestTask.Result);
        var series = KeepRecent(seriesTask.Result);
        var single = KeepRecent(singleTask.Result);
        var korean = KeepRecent(koreanTask.Result);
        var chinese = KeepRecent(chineseTask.Result);
        var statsMovies = items.Take(10).Concat(korean.Take(6)).Concat(chinese.Take(6)).ToList();
        ApplyCachedEpisodeStats(statsMovies);
        WarmEpisodeStats(statsMovies.Select(movie => movie.Slug));

        var userId = GetCurrentUserId() ?? 0;
        var history = _db.WatchHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.UpdatedAt)
            .Take(8)
            .ToList();

        ViewBag.History = history;
        ViewBag.Series = series;
        ViewBag.Single = single;
        ViewBag.Korean = korean;
        ViewBag.Chinese = chinese;
        return View(items);
    }

    public async Task<IActionResult> Search(string keyword, int page = 1)
    {
        var client = _http.CreateClient("OPhim");
        var items = string.IsNullOrWhiteSpace(keyword)
            ? new List<MovieItem>()
            : await GetMovieItems(client, $"tim-kiem?keyword={Uri.EscapeDataString(keyword)}&page={page}");

        ViewBag.Keyword = keyword;
        return View(items);
    }

    public async Task<IActionResult> DanhSach(string slug = "phim-moi-cap-nhat", int page = 1)
    {
        var client = _http.CreateClient("OPhim");
        var items = await GetMovieItems(client, $"danh-sach/{slug}?page={page}");

        ViewBag.Slug = slug;
        ViewBag.Title = GetListTitle(slug);
        return View("List", items);
    }

    public async Task<IActionResult> TheLoai(string slug, int page = 1)
    {
        var client = _http.CreateClient("OPhim");
        var items = await GetMovieItems(client, $"the-loai/{slug}?page={page}");

        ViewBag.Slug = slug;
        ViewBag.Title = slug.Replace("-", " ");
        return View("List", items);
    }

    public async Task<IActionResult> QuocGia(string slug, int page = 1)
    {
        var client = _http.CreateClient("OPhim");
        var items = await GetMovieItems(client, $"quoc-gia/{slug}?page={page}");

        ViewBag.Slug = slug;
        ViewBag.Title = GetCountryTitle(slug);
        return View("List", items);
    }

    private async Task<List<MovieItem>> GetMovieItems(HttpClient client, string path)
    {
        var cacheKey = $"movie-list:{path}";
        if (_cache.TryGetValue(cacheKey, out List<MovieItem>? cachedItems) && cachedItems != null)
        {
            return cachedItems;
        }

        try
        {
            var response = await client.GetStringAsync(path);

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            // API OPhim mới đặt danh sách phim ở data.items, còn code cũ từng đọc items ở root.
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("items", out var nestedItems))
            {
                var parsedItems = JsonSerializer.Deserialize<List<MovieItem>>(nestedItems.GetRawText()) ?? new();
                _cache.Set(cacheKey, parsedItems, TimeSpan.FromMinutes(5));
                return parsedItems;
            }

            if (root.TryGetProperty("items", out var items))
            {
                var parsedItems = JsonSerializer.Deserialize<List<MovieItem>>(items.GetRawText()) ?? new();
                _cache.Set(cacheKey, parsedItems, TimeSpan.FromMinutes(5));
                return parsedItems;
            }
        }
        catch
        {
            // Trang chủ vẫn render bình thường nếu API tạm lỗi.
        }

        return new();
    }

    private static List<MovieItem> KeepRecent(IEnumerable<MovieItem> movies)
    {
        return movies
            .Where(movie => movie.Year > 2010)
            .ToList();
    }

    private void ApplyCachedEpisodeStats(IEnumerable<MovieItem> movies)
    {
        foreach (var movie in movies)
        {
            if (string.IsNullOrWhiteSpace(movie.Slug)) continue;

            var cacheKey = $"episode-stats:{movie.Slug}";
            if (_cache.TryGetValue(cacheKey, out EpisodeStats? cachedStats) && cachedStats != null)
            {
                movie.VietsubEpisodeCount = cachedStats.Vietsub;
                movie.ThuyetMinhEpisodeCount = cachedStats.ThuyetMinh;
            }
        }
    }

    private void WarmEpisodeStats(IEnumerable<string> slugs)
    {
        var missingSlugs = slugs
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(slug => !_cache.TryGetValue($"episode-stats:{slug}", out EpisodeStats? _))
            .ToList();

        if (!missingSlugs.Any()) return;

        _ = Task.Run(async () =>
        {
            using var throttler = new SemaphoreSlim(3);
            var tasks = missingSlugs.Select(async slug =>
            {
                await throttler.WaitAsync();
                try
                {
                    await GetEpisodeStats(slug);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
        });
    }

    private async Task<EpisodeStats> GetEpisodeStats(string slug)
    {
        var cacheKey = $"episode-stats:{slug}";
        if (_cache.TryGetValue(cacheKey, out EpisodeStats? cachedStats) && cachedStats != null)
        {
            return cachedStats;
        }

        try
        {
            var client = _http.CreateClient("KKPhim");
            var response = await client.GetStringAsync($"phim/{slug}");
            var episodes = ParseEpisodes(response);

            var vietsub = 0;
            var thuyetMinh = 0;

            foreach (var server in episodes)
            {
                if (server.ServerData.Count == 0) continue;

                if (IsThuyetMinh(server.ServerName) ||
                    server.ServerData.Any(ep => IsThuyetMinh(ep.Filename)))
                {
                    thuyetMinh = Math.Max(thuyetMinh, server.ServerData.Count);
                    continue;
                }

                if (IsVietsub(server.ServerName) ||
                    server.ServerData.Any(ep => IsVietsub(ep.Filename)) ||
                    !IsThuyetMinh(server.ServerName))
                {
                    vietsub = Math.Max(vietsub, server.ServerData.Count);
                }
            }

            var stats = new EpisodeStats(vietsub, thuyetMinh);
            _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(20));
            return stats;
        }
        catch
        {
            var emptyStats = new EpisodeStats(0, 0);
            _cache.Set(cacheKey, emptyStats, TimeSpan.FromMinutes(5));
            return emptyStats;
        }
    }

    private static List<ServerGroup> ParseEpisodes(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("item", out var item) &&
            item.TryGetProperty("episodes", out var itemEpisodes))
        {
            return JsonSerializer.Deserialize<List<ServerGroup>>(itemEpisodes.GetRawText()) ?? new();
        }

        if (root.TryGetProperty("episodes", out var episodes))
        {
            return JsonSerializer.Deserialize<List<ServerGroup>>(episodes.GetRawText()) ?? new();
        }

        return new();
    }

    private static bool IsThuyetMinh(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Contains("Thuyết", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Thuyet", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Lồng", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Long", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsVietsub(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.Contains("Vietsub", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Viet Sub", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Phụ đề", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Phu de", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetListTitle(string slug)
    {
        return slug switch
        {
            "phim-moi-cap-nhat" => "Phim mới cập nhật",
            "phim-bo" => "Phim bộ",
            "phim-le" => "Phim lẻ",
            "hoat-hinh" => "Hoạt hình",
            "tv-shows" => "TV Shows",
            _ => slug.Replace("-", " ")
        };
    }

    private static string GetCountryTitle(string slug)
    {
        return slug switch
        {
            "viet-nam" => "Việt Nam",
            "han-quoc" => "Hàn Quốc",
            "trung-quoc" => "Trung Quốc",
            "nhat-ban" => "Nhật Bản",
            "thai-lan" => "Thái Lan",
            "au-my" => "Âu Mỹ",
            "anh" => "Anh",
            "phap" => "Pháp",
            "dai-loan" => "Đài Loan",
            "hong-kong" => "Hồng Kông",
            "an-do" => "Ấn Độ",
            "canada" => "Canada",
            _ => slug.Replace("-", " ")
        };
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

    private sealed record EpisodeStats(int Vietsub, int ThuyetMinh);
}
