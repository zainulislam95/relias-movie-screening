using MovieScreening.Api.Abstractions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MovieScreening.Api.Contracts;
using MovieScreening.Api.Data;

namespace MovieScreening.Api.Repositories;

public sealed class FavoriteRepository(FavoritesDbContext db) : IFavoriteRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken) =>
        await db.Database.EnsureCreatedAsync(cancellationToken);

    public async Task<Movie[]> GetAllAsync(CancellationToken cancellationToken)
    {
        var favorites = await db.Favorites.AsNoTracking()
            .OrderByDescending(f => f.AddedAtUtc).ThenBy(f => f.MovieId)
            .ToListAsync(cancellationToken);
        return favorites.Select(f => Deserialize(f.MovieJson)).ToArray();
    }

    public async Task<Movie?> GetByIdAsync(string movieId, CancellationToken cancellationToken)
    {
        var favorite = await db.Favorites.AsNoTracking()
            .SingleOrDefaultAsync(f => f.MovieId == movieId, cancellationToken);
        return favorite is null ? null : Deserialize(favorite.MovieJson);
    }

    public Task<bool> ExistsAsync(string movieId, CancellationToken cancellationToken) =>
        db.Favorites.AnyAsync(f => f.MovieId == movieId, cancellationToken);

    public async Task<bool> TryAddAsync(Movie movie, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(movie);
        var now = DateTime.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Favorites (MovieId, MovieJson, AddedAtUtc) VALUES ({movie.Id}, {json}, {now}) ON CONFLICT(MovieId) DO NOTHING",
            cancellationToken);
        return inserted == 1;
    }

    public async Task RemoveAsync(string movieId, CancellationToken cancellationToken) =>
        await db.Favorites.Where(f => f.MovieId == movieId).ExecuteDeleteAsync(cancellationToken);

    private static Movie Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Movie>(json)
                   ?? throw new InvalidOperationException(
                       "Stored movie data could not be deserialized.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Stored movie data contains invalid JSON.",
                ex);
        }
    }
}
