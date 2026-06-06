using LVDKMovie.Data;
using Microsoft.AspNetCore.Mvc;

namespace LVDKMovie.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
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
        var user = _db.AppUsers.FirstOrDefault(u => u.UserName == userName && u.Password == password);

        if (user == null)
        {
            ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
            return View();
        }

        HttpContext.Session.SetString("UserName", user.UserName);
        HttpContext.Session.SetString("DisplayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName);
        HttpContext.Session.SetString("AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? "/images/avatars/default-admin.jpg" : user.AvatarUrl);
        HttpContext.Session.SetInt32("UserId", user.Id);

        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        };

        Response.Cookies.Append("LVDKMovie.UserId", user.Id.ToString(), cookieOptions);
        Response.Cookies.Append("LVDKMovie.UserName", user.UserName, cookieOptions);
        Response.Cookies.Append("LVDKMovie.DisplayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName, cookieOptions);
        Response.Cookies.Append("LVDKMovie.AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? "/images/avatars/default-admin.jpg" : user.AvatarUrl, cookieOptions);

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
        HttpContext.Session.SetString("AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? "/images/avatars/default-admin.jpg" : user.AvatarUrl);
        HttpContext.Session.SetInt32("UserId", user.Id);
    }
}
