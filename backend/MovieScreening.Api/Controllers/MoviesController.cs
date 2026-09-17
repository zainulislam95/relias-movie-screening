using MovieScreening.Api.Abstractions;
using Microsoft.AspNetCore.Mvc;
using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Controllers;

[ApiController]
[Route("api/movies")]
public sealed class MoviesController(IMovieService movies) : ControllerBase
{
    [HttpGet]
    public Task<MoviePage> Search([FromQuery] MovieSearch search, CancellationToken cancellationToken) =>
        movies.SearchAsync(search, cancellationToken);
}
