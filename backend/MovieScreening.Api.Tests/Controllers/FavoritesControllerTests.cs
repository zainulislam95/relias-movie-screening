using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Controllers;
using MovieScreening.Api.Contracts;
using NUnit.Framework;

namespace MovieScreening.Api.Tests;

[Category("Unit")]
public sealed class FavoritesControllerTests
{
    [Test]
    public async Task Get_returns_favorites_from_service()
    {
        var mock = new Mock<IFavoriteService>();
        var expected = new[] { new Movie("1", "Title", "Title", 2020, "Plot", 50, null, new[] { new Genre("drama", "Drama") }) };
        mock.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ctrl = new FavoritesController(mock.Object);
        var result = await ctrl.Get(CancellationToken.None);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetById_returns_notfound_when_null()
    {
        var mock = new Mock<IFavoriteService>();
        mock.Setup(s => s.GetByIdAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync((Movie?)null);
        var ctrl = new FavoritesController(mock.Object);
        var action = await ctrl.GetById("123", CancellationToken.None);
        Assert.That(action.Result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetById_returns_ok_when_found()
    {
        var movie = new Movie("123", "A", "A", 2020, "", 0, null, Array.Empty<Genre>());
        var mock = new Mock<IFavoriteService>();
        mock.Setup(s => s.GetByIdAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(movie);
        var ctrl = new FavoritesController(mock.Object);
        var action = await ctrl.GetById("123", CancellationToken.None);
        Assert.That(action.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)action.Result!;
        Assert.That(ok.Value, Is.SameAs(movie));
    }

    [TestCase(AddFavoriteResult.Created, typeof(CreatedAtActionResult))]
    [TestCase(AddFavoriteResult.AlreadyExists, typeof(NoContentResult))]
    [TestCase(AddFavoriteResult.NotFound, typeof(NotFoundResult))]
    public async Task Add_returns_expected_status(AddFavoriteResult serviceResult, Type expectedType)
    {
        var mock = new Mock<IFavoriteService>();
        mock.Setup(s => s.AddAsync("123", It.IsAny<CancellationToken>())).ReturnsAsync(serviceResult);
        var ctrl = new FavoritesController(mock.Object);
        var result = await ctrl.Add("123", CancellationToken.None);
        Assert.That(result, Is.TypeOf(expectedType));
    }

    [Test]
    public async Task Remove_calls_service_and_returns_nocontent()
    {
        var mock = new Mock<IFavoriteService>();
        mock.Setup(s => s.RemoveAsync("123", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
        var ctrl = new FavoritesController(mock.Object);
        var result = await ctrl.Remove("123", CancellationToken.None);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        mock.Verify();
    }
}
