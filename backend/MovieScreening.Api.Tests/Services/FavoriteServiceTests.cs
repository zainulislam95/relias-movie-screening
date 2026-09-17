using System.Threading;
using System.Threading.Tasks;
using Moq;
using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Services;
using MovieScreening.Api.Contracts;
using MovieScreening.Api.Exceptions;
using NUnit.Framework;

namespace MovieScreening.Api.Tests;

[Category("Unit")]
public sealed class FavoriteServiceTests
{
    private static readonly Movie Sample = new("123", "A Movie", "A Movie", 2020,
        "Plot", 85, null, new[] { new Genre("drama", "Drama") });

    [Test]
    public async Task Existing_favorite_skips_movie_lookup_and_insert()
    {
        var repo = new Mock<IFavoriteRepository>();
        repo.Setup(r => r.ExistsAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var movies = new Mock<IMovieService>(MockBehavior.Strict);
        var svc = new FavoriteService(repo.Object, movies.Object);
        var result = await svc.AddAsync("123", CancellationToken.None);
        Assert.That(result, Is.EqualTo(AddFavoriteResult.AlreadyExists));
        movies.Verify(m => m.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.TryAddAsync(It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Unknown_movie_is_not_inserted()
    {
        var repo = new Mock<IFavoriteRepository>();
        repo.Setup(r => r.ExistsAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.TryAddAsync(It.IsAny<Movie>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var movies = new Mock<IMovieService>();
        movies.Setup(m => m.GetAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync((Movie?)null);
        var svc = new FavoriteService(repo.Object, movies.Object);
        var result = await svc.AddAsync("123", CancellationToken.None);
        Assert.That(result, Is.EqualTo(AddFavoriteResult.NotFound));
        repo.Verify(r => r.TryAddAsync(It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Insert_result_distinguishes_new_favorite_from_concurrent_duplicate()
    {
        var repo = new Mock<IFavoriteRepository>();
        repo.Setup(r => r.ExistsAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.TryAddAsync(It.IsAny<Movie>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var movies = new Mock<IMovieService>();
        movies.Setup(m => m.GetAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(Sample);
        var svc = new FavoriteService(repo.Object, movies.Object);
        var result = await svc.AddAsync("123", CancellationToken.None);
        Assert.That(result, Is.EqualTo(AddFavoriteResult.Created));
        repo.Verify(r => r.TryAddAsync(It.Is<Movie>(m => m.Id == Sample.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Wrong_movie_from_provider_is_not_inserted()
    {
        var repo = new Mock<IFavoriteRepository>();
        repo.Setup(r => r.ExistsAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var movies = new Mock<IMovieService>();
        movies.Setup(m => m.GetAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(Sample with { Id = "456" });
        var svc = new FavoriteService(repo.Object, movies.Object);
        var ex = Assert.ThrowsAsync<CatalogException>(() => svc.AddAsync("123", CancellationToken.None));
        Assert.That(ex!.StatusCode, Is.EqualTo(502));
        repo.Verify(r => r.TryAddAsync(It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Provider_failure_does_not_write_to_repository()
    {
        var repo = new Mock<IFavoriteRepository>();
        repo.Setup(r => r.ExistsAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var movies = new Mock<IMovieService>();
        movies.Setup(m => m.GetAsync("123", It.IsAny<CancellationToken>())).ThrowsAsync(new CatalogException(503, "Unavailable"));
        var svc = new FavoriteService(repo.Object, movies.Object);
        Assert.ThrowsAsync<CatalogException>(() => svc.AddAsync("123", CancellationToken.None));
        repo.Verify(r => r.TryAddAsync(It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Saved_movies_can_be_read_and_removed_without_provider_access()
    {
        var repo = new Mock<IFavoriteRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Sample });
        repo.Setup(r => r.GetByIdAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(Sample);
        repo.Setup(r => r.RemoveAsync("123", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
        var movies = new Mock<IMovieService>(MockBehavior.Strict);
        var svc = new FavoriteService(repo.Object, movies.Object);
        Assert.That(await svc.GetAsync(CancellationToken.None), Is.EqualTo(new[] { Sample }));
        Assert.That(await svc.GetByIdAsync("123", CancellationToken.None), Is.SameAs(Sample));
        await svc.RemoveAsync("123", CancellationToken.None);
        repo.Verify();
        movies.Verify(m => m.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
