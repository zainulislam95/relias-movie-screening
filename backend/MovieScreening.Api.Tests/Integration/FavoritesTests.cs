using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Exceptions;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MovieScreening.Api.Contracts;
using NUnit.Framework;

namespace MovieScreening.Api.Tests;

[Category("Integration")]
public sealed class FavoritesTests
{
    private string databasePath = null!;
    private static readonly Movie Sample = new("123", "A Movie", "A Movie", 2020,
        "A saved plot", 85, "https://example.com/poster.jpg", [new Genre("drama", "Drama")]);

    [SetUp]
    public void SetUp() => databasePath = Path.Combine(Path.GetTempPath(), $"movie-favorites-{Guid.NewGuid():N}.db");

    [TearDown]
    public void TearDown() => File.Delete(databasePath);

    private sealed class Catalog : IMovieCatalog
    {
        public int Calls;
        public bool Fail;
        public Func<Task>? BeforeReturn;
        public async Task<Movie?> GetAsync(string id, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (Fail) throw new CatalogException(503, "Provider unavailable.");
            if (BeforeReturn is not null) await BeforeReturn();
            return id == Sample.Id ? Sample : null;
        }
        public Task<MoviePage> SearchAsync(MovieSearch search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Genre[]> GenresAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Factory(string path, Catalog catalog) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Favorites"] = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString(),
                ["Streaming:Country"] = "de", ["Streaming:ApiKey"] = ""
            }))
            .ConfigureServices(services =>
            {
                services.RemoveAll<IMovieCatalog>();
                services.AddSingleton<IMovieCatalog>(catalog);
            });
    }

    [Test]
    public async Task Favorites_survive_application_restart_and_provider_outage()
    {
        await using (var app = new Factory(databasePath, new Catalog()))
        {
            using var client = app.CreateClient();
            using var response = await client.PutAsync("/api/favorites/123", null);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(response.Headers.Location?.ToString(), Does.EndWith("/api/favorites/123"));
            var created = await client.GetFromJsonAsync<Movie>(response.Headers.Location);
            Assert.That(created!.Id, Is.EqualTo(Sample.Id));
        }
        var offline = new Catalog { Fail = true };
        await using var restarted = new Factory(databasePath, offline);
        using var restartedClient = restarted.CreateClient();
        var saved = await restartedClient.GetFromJsonAsync<Movie[]>("/api/favorites");
        Assert.Multiple(() =>
        {
            Assert.That(saved, Has.Length.EqualTo(1));
            Assert.That(saved![0].Title, Is.EqualTo(Sample.Title));
            Assert.That(saved[0].Genres, Is.EqualTo(Sample.Genres));
            Assert.That(saved[0].PosterUrl, Is.EqualTo(Sample.PosterUrl));
            Assert.That(offline.Calls, Is.Zero);
        });
    }

    [Test]
    public async Task Repeated_add_and_delete_are_idempotent()
    {
        var catalog = new Catalog();
        await using var app = new Factory(databasePath, catalog);
        using var client = app.CreateClient();
        using var first = await client.PutAsync("/api/favorites/123", null);
        catalog.Fail = true;
        using var second = await client.PutAsync("/api/favorites/123", null);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(catalog.Calls, Is.EqualTo(1));
        Assert.That(await client.GetFromJsonAsync<Movie[]>("/api/favorites"), Has.Length.EqualTo(1));
        using var removed = await client.DeleteAsync("/api/favorites/123");
        using var removedAgain = await client.DeleteAsync("/api/favorites/123");
        Assert.That(removed.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(removedAgain.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await client.GetFromJsonAsync<Movie[]>("/api/favorites"), Is.Empty);
    }

    [Test]
    public async Task Concurrent_adds_store_one_row()
    {
        var arrived = 0;
        var bothArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new Catalog
        {
            BeforeReturn = async () =>
            {
                if (Interlocked.Increment(ref arrived) == 2) bothArrived.SetResult();
                await bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        };
        await using var app = new Factory(databasePath, catalog);
        using var client = app.CreateClient();
        var responses = await Task.WhenAll(client.PutAsync("/api/favorites/123", null), client.PutAsync("/api/favorites/123", null));
        try { Assert.That(responses.Select(r => r.StatusCode), Is.EquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.NoContent })); }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.That(await client.GetFromJsonAsync<Movie[]>("/api/favorites"), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Unknown_movie_is_not_saved()
    {
        await using var app = new Factory(databasePath, new Catalog());
        using var client = app.CreateClient();
        using var response = await client.PutAsync("/api/favorites/999", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await client.GetFromJsonAsync<Movie[]>("/api/favorites"), Is.Empty);
    }

    [Test]
    public async Task Reading_an_absent_favorite_returns_not_found_without_provider_access()
    {
        var catalog = new Catalog { Fail = true };
        await using var app = new Factory(databasePath, catalog);
        using var client = app.CreateClient();
        using var response = await client.GetAsync("/api/favorites/999");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(catalog.Calls, Is.Zero);
    }

    [Test]
    public async Task Provider_failure_does_not_create_a_favorite()
    {
    }
}
