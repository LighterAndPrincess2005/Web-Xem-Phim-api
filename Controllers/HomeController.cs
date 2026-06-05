using LVDKMovie.Data;
using LVDKMovie.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LVDKMovie.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _http;
    private readonly AppDbContext _db;

    public HomeController(IHttpClientFactory http, AppDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var client = _http.CreateClient("OPhim");
        var items = await GetMovieItems(client, "danh-sach/phim-moi-cap-nhat?page=1");
        var series = await GetMovieItems(client, "danh-sach/phim-bo?page=1");
        var single = await GetMovieItems(client, "danh-sach/phim-le?page=1");

        var history = _db.WatchHistories
            .OrderByDescending(h => h.UpdatedAt)
            .Take(8)
            .ToList();

        ViewBag.History = history;
        ViewBag.Series = series;
        ViewBag.Single = single;
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
        ViewBag.Title = slug.Replace("-", " ");
        return View("List", items);
    }

    private static async Task<List<MovieItem>> GetMovieItems(HttpClient client, string path)
    {
        try
        {
            var response = await client.GetStringAsync(path);

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            // API OPhim mới đặt danh sách phim ở data.items, còn code cũ từng đọc items ở root.
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("items", out var nestedItems))
            {
                return JsonSerializer.Deserialize<List<MovieItem>>(nestedItems.GetRawText()) ?? new();
            }

            if (root.TryGetProperty("items", out var items))
            {
                return JsonSerializer.Deserialize<List<MovieItem>>(items.GetRawText()) ?? new();
            }
        }
        catch
        {
            // Trang chủ vẫn render bình thường nếu API tạm lỗi.
        }

        return new();
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
}
