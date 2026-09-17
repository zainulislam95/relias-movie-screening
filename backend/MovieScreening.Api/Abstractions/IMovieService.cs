using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Abstractions;

public interface IMovieService
{
    Task<MoviePage> SearchAsync(MovieSearch search, CancellationToken cancellationToken);
    Task<Genre[]> GenresAsync(CancellationToken cancellationToken);
    Task<Movie?> GetAsync(string id, CancellationToken cancellationToken);
}