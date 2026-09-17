using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Services;

public sealed class MovieService(IMovieCatalog catalog) : IMovieService
{
    public Task<MoviePage> SearchAsync(MovieSearch search, CancellationToken cancellationToken) =>
        catalog.SearchAsync(search, cancellationToken);

    public Task<Genre[]> GenresAsync(CancellationToken cancellationToken) =>
        catalog.GenresAsync(cancellationToken);

    public Task<Movie?> GetAsync(string id, CancellationToken cancellationToken) =>
        catalog.GetAsync(id, cancellationToken);
}