using Microsoft.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore;

namespace TapInAuth.Samples.Mvc.Data;

/// <summary>
/// The host application's EF Core DbContext. TapInAuth's four tables are added by calling
/// <c>modelBuilder.ApplyTapInAuthConfiguration()</c> in <see cref="OnModelCreating"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyTapInAuthConfiguration();
    }
}
