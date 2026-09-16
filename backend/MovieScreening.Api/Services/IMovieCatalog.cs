using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Services;

public interface IMovieCatalog
{
    Task<MoviePage> SearchAsync(MovieSearch search, CancellationToken cancellationToken);
    Task<Genre[]> GenresAsync(CancellationToken cancellationToken);
    Task<Movie?> GetAsync(string id, CancellationToken cancellationToken);
}
