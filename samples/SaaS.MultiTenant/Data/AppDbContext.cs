using Microsoft.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore;

namespace TapInAuth.Samples.SaaS.Data;

/// <summary>The host's DbContext. TapInAuth tables join the same database, each with TenantId.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyTapInAuthConfiguration();
    }
}
