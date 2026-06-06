using LVDKMovie.Data;
using LVDKMovie.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

const string defaultAvatarUrl = "/images/avatars/default-admin.jpg";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromDays(14);
});
builder.Services.AddHttpClient("OPhim", client =>
{
    client.BaseAddress = new Uri("https://ophim1.com/v1/api/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(6);
});
builder.Services.AddHttpClient("KKPhim", client =>
{
    client.BaseAddress = new Uri("https://phimapi.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(4);
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=lvdkmovie.db"));

var app = builder.Build();

// Auto migrate DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS AppUsers (
            Id INTEGER NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY AUTOINCREMENT,
            UserName TEXT NOT NULL,
            Password TEXT NOT NULL,
            DisplayName TEXT NOT NULL,
            AvatarUrl TEXT NOT NULL DEFAULT '/images/avatars/default-admin.jpg'
        );
        """);
    try
    {
        db.Database.ExecuteSqlRaw("""
            ALTER TABLE AppUsers
            ADD COLUMN AvatarUrl TEXT NOT NULL DEFAULT '/images/avatars/default-admin.jpg';
            """);
    }
    catch
    {
        // Column already exists in upgraded local databases.
    }
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS FavoriteMovies (
            Id INTEGER NOT NULL CONSTRAINT PK_FavoriteMovies PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Slug TEXT NOT NULL,
            Title TEXT NOT NULL,
            Thumb TEXT NOT NULL,
            Poster TEXT NOT NULL,
            Year INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw("""
        CREATE UNIQUE INDEX IF NOT EXISTS IX_FavoriteMovies_UserId_Slug
        ON FavoriteMovies (UserId, Slug);
        """);

    AddColumnIfMissing(db, "WatchHistories", "UserId", "INTEGER NOT NULL DEFAULT 0");
    AddColumnIfMissing(db, "WatchHistories", "EpisodeSlug", "TEXT NOT NULL DEFAULT ''");
    AddColumnIfMissing(db, "WatchHistories", "ServerName", "TEXT NOT NULL DEFAULT ''");

    if (!db.AppUsers.Any(u => u.UserName == "admin"))
    {
        db.AppUsers.Add(new AppUser
        {
            UserName = "admin",
            Password = PasswordService.Hash("admin"),
            DisplayName = "Admin",
            AvatarUrl = defaultAvatarUrl
        });
        db.SaveChanges();
    }

    var usersWithoutAvatar = db.AppUsers
        .Where(u => string.IsNullOrWhiteSpace(u.AvatarUrl))
        .ToList();

    foreach (var user in usersWithoutAvatar)
    {
        user.AvatarUrl = defaultAvatarUrl;
    }

    var admin = db.AppUsers.FirstOrDefault(u => u.UserName == "admin");
    if (admin != null && admin.AvatarUrl != defaultAvatarUrl)
    {
        admin.AvatarUrl = defaultAvatarUrl;
    }

    var usersWithPlainPasswords = db.AppUsers
        .Where(u => !u.Password.StartsWith("pbkdf2$"))
        .ToList();

    foreach (var user in usersWithPlainPasswords)
    {
        user.Password = PasswordService.Hash(user.Password);
    }

    if (usersWithoutAvatar.Any() || admin != null || usersWithPlainPasswords.Any())
    {
        db.SaveChanges();
    }

    if (admin != null)
    {
        db.Database.ExecuteSqlRaw("UPDATE WatchHistories SET UserId = {0} WHERE UserId = 0", admin.Id);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https:; " +
        "frame-src https:; " +
        "connect-src 'self' https:; " +
        "media-src 'self' https: blob:";

    if (context.Request.IsHttps)
    {
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});
app.UseSession();

app.MapControllerRoute(
    name: "watch",
    pattern: "xem/{slug}/{episode?}",
    defaults: new { controller = "Movie", action = "Watch" });

app.MapControllerRoute(
    name: "movie",
    pattern: "phim/{slug}",
    defaults: new { controller = "Movie", action = "Detail" });

app.MapControllerRoute(
    name: "list",
    pattern: "danh-sach/{slug}",
    defaults: new { controller = "Home", action = "DanhSach" });

app.MapControllerRoute(
    name: "category",
    pattern: "the-loai/{slug}",
    defaults: new { controller = "Home", action = "TheLoai" });

app.MapControllerRoute(
    name: "country",
    pattern: "quoc-gia/{slug}",
    defaults: new { controller = "Home", action = "QuocGia" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static void AddColumnIfMissing(DbContext db, string tableName, string columnName, string columnDefinition)
{
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnDefinition + ";");
    }
    catch
    {
        // Column already exists in upgraded local databases.
    }
}
