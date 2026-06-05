using LVDKMovie.Data;
using Microsoft.EntityFrameworkCore;

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
            DisplayName TEXT NOT NULL
        );
        """);

    if (!db.AppUsers.Any(u => u.UserName == "admin"))
    {
        db.AppUsers.Add(new AppUser
        {
            UserName = "admin",
            Password = "admin",
            DisplayName = "Admin"
        });
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
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
