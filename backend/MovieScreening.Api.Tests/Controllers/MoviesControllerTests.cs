using System.Threading;
using System.Threading.Tasks;
using Moq;
using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Controllers;
using MovieScreening.Api.Contracts;
using NUnit.Framework;

namespace MovieScreening.Api.Tests;

public sealed class MoviesControllerTests
{
    [Test]
    public async Task Search_forwards_search_to_service_and_returns_page()
    {
        var search = new MovieSearch { Title = "Test", Year = 2020, Genre = "drama", SortBy = "year", Direction = "asc" };
        var expected = new MoviePage(new[] { new Movie("1", "T", "T", 2020, "", 10, null, new[] { new Genre("drama", "Drama") }) }, false, null, "note");
        var mock = new Mock<IMovieService>();
        mock.Setup(s => s.SearchAsync(It.IsAny<MovieSearch>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ctrl = new MoviesController(mock.Object);
        var result = await ctrl.Search(search, CancellationToken.None);
        Assert.That(result, Is.SameAs(expected));
        mock.Verify(s => s.SearchAsync(search, It.IsAny<CancellationToken>()), Times.Once);
    }
}
