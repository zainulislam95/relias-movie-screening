using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MovieScreening.Api.Contracts;
using MovieScreening.Api.Data;

namespace MovieScreening.Api.Services;

public enum AddFavoriteResult { Created, AlreadyExists, NotFound }

public sealed class FavoriteService(FavoritesDbContext db, IMovieCatalog catalog)
{
    public async Task<Movie[]> GetAsync(CancellationToken cancellationToken)
    {
        var favorites = await db.Favorites.AsNoTracking()
            .OrderByDescending(f => f.AddedAtUtc).ThenBy(f => f.MovieId)
            .ToListAsync(cancellationToken);
        return favorites.Select(f => JsonSerializer.Deserialize<Movie>(f.MovieJson)
            ?? throw new InvalidOperationException("Stored favorite is empty.")).ToArray();
    }

    public async Task<Movie?> GetByIdAsync(string movieId, CancellationToken cancellationToken)
    {
        var favorite = await db.Favorites.AsNoTracking().SingleOrDefaultAsync(f => f.MovieId == movieId, cancellationToken);
        return favorite is null ? null : JsonSerializer.Deserialize<Movie>(favorite.MovieJson);
    }

    public async Task<AddFavoriteResult> AddAsync(string movieId, CancellationToken cancellationToken)
    {
        if (await db.Favorites.AnyAsync(f => f.MovieId == movieId, cancellationToken)) return AddFavoriteResult.AlreadyExists;
        var movie = await catalog.GetAsync(movieId, cancellationToken);
        if (movie is null) return AddFavoriteResult.NotFound;
        if (movie.Id != movieId) throw new CatalogException(502, "The movie provider returned a different movie.");
        var json = JsonSerializer.Serialize(movie);
        var now = DateTime.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Favorites (MovieId, MovieJson, AddedAtUtc) VALUES ({movie.Id}, {json}, {now}) ON CONFLICT(MovieId) DO NOTHING",
            cancellationToken);
        return inserted == 1 ? AddFavoriteResult.Created : AddFavoriteResult.AlreadyExists;
    }

    public Task<int> RemoveAsync(string movieId, CancellationToken cancellationToken) =>
        db.Favorites.Where(f => f.MovieId == movieId).ExecuteDeleteAsync(cancellationToken);
}
