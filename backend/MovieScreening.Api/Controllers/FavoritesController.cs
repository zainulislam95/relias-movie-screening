using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MovieScreening.Api.Contracts;
using MovieScreening.Api.Services;

namespace MovieScreening.Api.Controllers;

[ApiController]
[Route("api/favorites")]
public sealed class FavoritesController(FavoriteService favorites) : ControllerBase
{
    [HttpGet]
    public Task<Movie[]> Get(CancellationToken cancellationToken) => favorites.GetAsync(cancellationToken);

    [HttpGet("{movieId}")]
    public async Task<ActionResult<Movie>> GetById([RegularExpression("^[0-9]{1,20}$")] string movieId,
        CancellationToken cancellationToken)
    {
        var movie = await favorites.GetByIdAsync(movieId, cancellationToken);
        return movie is null ? NotFound() : Ok(movie);
    }

    [HttpPut("{movieId}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add([RegularExpression("^[0-9]{1,20}$")] string movieId,
        CancellationToken cancellationToken) =>
        await favorites.AddAsync(movieId, cancellationToken) switch
        {
            AddFavoriteResult.Created => CreatedAtAction(nameof(GetById), new { movieId }, null),
            AddFavoriteResult.AlreadyExists => NoContent(),
            _ => NotFound()
        };

    [HttpDelete("{movieId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove([RegularExpression("^[0-9]{1,20}$")] string movieId,
        CancellationToken cancellationToken)
    {
        await favorites.RemoveAsync(movieId, cancellationToken);
        return NoContent();
    }
}
