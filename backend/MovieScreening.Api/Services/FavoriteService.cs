using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Exceptions;
using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Services;

public sealed class FavoriteService(IFavoriteRepository repository, IMovieService movies) : IFavoriteService
{
    public Task<Movie[]> GetAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);

    public Task<Movie?> GetByIdAsync(string movieId, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(movieId, cancellationToken);

    public async Task<AddFavoriteResult> AddAsync(string movieId, CancellationToken cancellationToken)
    {
        if (await repository.ExistsAsync(movieId, cancellationToken)) return AddFavoriteResult.AlreadyExists;

        var movie = await movies.GetAsync(movieId, cancellationToken);
        if (movie is null) return AddFavoriteResult.NotFound;
        if (movie.Id != movieId) throw new CatalogException(502, "The movie provider returned a different movie.");

        var inserted = await repository.TryAddAsync(movie, cancellationToken);
        return inserted ? AddFavoriteResult.Created : AddFavoriteResult.AlreadyExists;
    }

    public Task RemoveAsync(string movieId, CancellationToken cancellationToken) =>
        repository.RemoveAsync(movieId, cancellationToken);
}