using Microsoft.AspNetCore.Mvc;
using MovieScreening.Api.Contracts;
using MovieScreening.Api.Services;

namespace MovieScreening.Api.Controllers;

[ApiController]
[Route("api/movies")]
public sealed class MoviesController(IMovieCatalog catalog) : ControllerBase
{
    [HttpGet]
    public Task<MoviePage> Search([FromQuery] MovieSearch search, CancellationToken cancellationToken) =>
        catalog.SearchAsync(search, cancellationToken);
}
