using MovieScreening.Api.Abstractions;
using Microsoft.AspNetCore.Mvc;
using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Controllers;

[ApiController]
[Route("api/genres")]
public sealed class GenresController(IMovieService movies) : ControllerBase
{
    [HttpGet]
    public Task<Genre[]> Get(CancellationToken cancellationToken) => movies.GenresAsync(cancellationToken);
}
