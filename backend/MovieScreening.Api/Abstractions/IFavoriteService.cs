using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Abstractions;

public interface IFavoriteService
{
    Task<Movie[]> GetAsync(CancellationToken cancellationToken);
    Task<Movie?> GetByIdAsync(string movieId, CancellationToken cancellationToken);
    Task<AddFavoriteResult> AddAsync(string movieId, CancellationToken cancellationToken);
    Task RemoveAsync(string movieId, CancellationToken cancellationToken);
}