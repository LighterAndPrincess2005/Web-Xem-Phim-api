using LVDKMovie.Data;
using LVDKMovie.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace LVDKMovie.Controllers;

public class AccountController : Controller
{
    private const string DefaultAvatarUrl = "/images/avatars/default-admin.jpg";
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public AccountController(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public IActionResult Login()
    {
        RestoreRememberedUser();

        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public IActionResult Login(string userName, string password)
    {
        userName = (userName ?? "").Trim();
        password ??= "";

        var rateLimitKey = GetLoginRateLimitKey(userName);
        var failedCount = _cache.Get<int>(rateLimitKey);
        if (failedCount >= 5)
        {
            ViewBag.Error = "Bạn nhập sai quá nhiều lần. Thử lại sau vài phút.";
            return View();
        }

        var user = _db.AppUsers.FirstOrDefault(u => u.UserName == userName);
        if (user == null || !PasswordService.Verify(password, user.Password))
        {
            _cache.Set(rateLimitKey, failedCount + 1, TimeSpan.FromMinutes(5));
            ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
            return View();
        }

        if (!PasswordService.IsHash(user.Password))
        {
            user.Password = PasswordService.Hash(password);
            _db.SaveChanges();
        }

        _cache.Remove(rateLimitKey);
        HttpContext.Session.SetString("UserName", user.UserName);
        HttpContext.Session.SetString("DisplayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName);
        HttpContext.Session.SetString("AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? DefaultAvatarUrl : user.AvatarUrl);
        HttpContext.Session.SetInt32("UserId", user.Id);

        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        };

        Response.Cookies.Append("LVDKMovie.UserId", user.Id.ToString(), cookieOptions);
        Response.Cookies.Append("LVDKMovie.UserName", user.UserName, cookieOptions);
        Response.Cookies.Append("LVDKMovie.DisplayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName, cookieOptions);
        Response.Cookies.Append("LVDKMovie.AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? DefaultAvatarUrl : user.AvatarUrl, cookieOptions);

        return RedirectToAction("Index", "Home");
    }

    public IActionResult Favorites()
    {
        RestoreRememberedUser();

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToAction("Login");
        }

        var items = _db.FavoriteMovies
            .Where(f => f.UserId == userId.Value)
            .OrderByDescending(f => f.CreatedAt)
            .ToList();

        return View(items);
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        Response.Cookies.Delete("LVDKMovie.UserId");
        Response.Cookies.Delete("LVDKMovie.UserName");
        Response.Cookies.Delete("LVDKMovie.DisplayName");
        Response.Cookies.Delete("LVDKMovie.AvatarUrl");
        return RedirectToAction("Index", "Home");
    }

    private void RestoreRememberedUser()
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserName"))) return;

        if (!Request.Cookies.TryGetValue("LVDKMovie.UserId", out var userIdValue) ||
            !int.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var user = _db.AppUsers.Find(userId);
        if (user == null) return;

        HttpContext.Session.SetString("UserName", user.UserName);
        HttpContext.Session.SetString("DisplayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName);
        HttpContext.Session.SetString("AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? DefaultAvatarUrl : user.AvatarUrl);
        HttpContext.Session.SetInt32("UserId", user.Id);
    }

    private string GetLoginRateLimitKey(string userName)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"login-fail:{ip}:{userName.ToLowerInvariant()}";
    }
}
