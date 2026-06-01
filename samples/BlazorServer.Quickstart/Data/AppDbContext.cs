using Microsoft.EntityFrameworkCore;
using TapInAuth.Store.EntityFrameworkCore;

namespace TapInAuth.Samples.BlazorServer.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyTapInAuthConfiguration();
    }
}
