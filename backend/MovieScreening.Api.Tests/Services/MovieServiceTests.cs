using System.Threading;
using System.Threading.Tasks;
using Moq;
using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Services;
using MovieScreening.Api.Contracts;
using NUnit.Framework;

namespace MovieScreening.Api.Tests;

public sealed class MovieServiceTests
{
    [Test]
    public async Task Search_forwards_to_catalog()
    {
        var expected = new MoviePage(Array.Empty<Movie>(), false, null);
        var mock = new Mock<IMovieCatalog>();
        mock.Setup(m => m.SearchAsync(It.IsAny<MovieSearch>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var service = new MovieService(mock.Object);
        var result = await service.SearchAsync(new MovieSearch(), CancellationToken.None);
        Assert.That(result, Is.SameAs(expected));
        mock.Verify(m => m.SearchAsync(It.IsAny<MovieSearch>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Genres_forwards_to_catalog()
    {
        var expected = new[] { new Genre("drama", "Drama") };
        var mock = new Mock<IMovieCatalog>();
        mock.Setup(m => m.GenresAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var service = new MovieService(mock.Object);
        var result = await service.GenresAsync(CancellationToken.None);
        Assert.That(result, Is.SameAs(expected));
        mock.Verify(m => m.GenresAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Get_forwards_to_catalog()
    {
        var expected = new Movie("1", "T", "T", 2020, "", 0, null, Array.Empty<Genre>());
        var mock = new Mock<IMovieCatalog>();
        mock.Setup(m => m.GetAsync("1", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var service = new MovieService(mock.Object);
        var result = await service.GetAsync("1", CancellationToken.None);
        Assert.That(result, Is.SameAs(expected));
        mock.Verify(m => m.GetAsync("1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
