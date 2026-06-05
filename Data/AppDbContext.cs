using Microsoft.EntityFrameworkCore;

namespace LVDKMovie.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<WatchHistory> WatchHistories { get; set; }
    public DbSet<AppUser> AppUsers { get; set; }
}

public class WatchHistory
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Episode { get; set; } = "";
    public string Thumb { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
