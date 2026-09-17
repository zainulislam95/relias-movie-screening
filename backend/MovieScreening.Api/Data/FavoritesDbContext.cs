using Microsoft.EntityFrameworkCore;
using MovieScreening.Api.Models;

namespace MovieScreening.Api.Data;

public sealed class FavoritesDbContext(DbContextOptions<FavoritesDbContext> options) : DbContext(options)
{
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Favorite>().HasKey(f => f.MovieId);
        modelBuilder.Entity<Favorite>().Property(f => f.MovieId).HasMaxLength(20);
        modelBuilder.Entity<Favorite>().Property(f => f.MovieJson).IsRequired();
    }
}
