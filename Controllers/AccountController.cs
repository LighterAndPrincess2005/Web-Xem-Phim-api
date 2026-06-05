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

        return RedirectToAction("Index", "Home");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
