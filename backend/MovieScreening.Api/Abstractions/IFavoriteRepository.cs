using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Abstractions;

public interface IFavoriteRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<Movie[]> GetAllAsync(CancellationToken cancellationToken);
    Task<Movie?> GetByIdAsync(string movieId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string movieId, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(Movie movie, CancellationToken cancellationToken);
    Task RemoveAsync(string movieId, CancellationToken cancellationToken);
}