using System.Threading;
using System.Threading.Tasks;
using Moq;
using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Controllers;
using MovieScreening.Api.Contracts;
using NUnit.Framework;

namespace MovieScreening.Api.Tests;

public sealed class GenresControllerTests
{
    [Test]
    public async Task Get_returns_genres_from_service()
    {
        var expected = new[] { new Genre("drama", "Drama"), new Genre("comedy", "Comedy") };
        var mock = new Mock<IMovieService>();
        mock.Setup(s => s.GenresAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ctrl = new GenresController(mock.Object);
        var result = await ctrl.Get(CancellationToken.None);
        Assert.That(result, Is.EqualTo(expected));
    }
}
