using LVDKMovie.Data;
using Microsoft.EntityFrameworkCore;

const string defaultAvatarUrl = "/images/avatars/default-admin.jpg";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddHttpClient("OPhim", client =>
{
    client.BaseAddress = new Uri("https://ophim1.com/v1/api/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddHttpClient("KKPhim", client =>
{
    client.BaseAddress = new Uri("https://phimapi.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
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

    if (!db.AppUsers.Any(u => u.UserName == "admin"))
    {
        db.AppUsers.Add(new AppUser
        {
            UserName = "admin",
            Password = "admin",
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

    if (usersWithoutAvatar.Any() || admin != null)
    {
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
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
