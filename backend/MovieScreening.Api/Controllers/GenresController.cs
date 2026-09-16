using Microsoft.AspNetCore.Mvc;
using MovieScreening.Api.Contracts;
using MovieScreening.Api.Services;

namespace MovieScreening.Api.Controllers;

[ApiController]
[Route("api/genres")]
public sealed class GenresController(IMovieCatalog catalog) : ControllerBase
{
    [HttpGet]
    public Task<Genre[]> Get(CancellationToken cancellationToken) => catalog.GenresAsync(cancellationToken);
}
