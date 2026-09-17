using Moq;
using Moq.Protected;
using MovieScreening.Api.Configuration;
using MovieScreening.Api.Integrations.StreamingAvailability;
using MovieScreening.Api.Exceptions;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MovieScreening.Api.Contracts;
using NUnit.Framework;

namespace MovieScreening.Api.Tests;

[Category("Unit")]
public sealed class CatalogTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static StreamingAvailabilityClient Client(HttpMessageHandler handler, string key = "test-key") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.movieofthenight.com/v4/") },
            Options.Create(new StreamingOptions { ApiKey = key, Country = "de" }));

    [Test]
    public async Task Browsing_forwards_filters_and_preserves_opaque_cursor()
    {
        using var handler = new StubHandler((request, _) =>
        {
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            Assert.Multiple(() =>
            {
                Assert.That(request.RequestUri.AbsolutePath, Is.EqualTo("/v4/shows/search/filters"));
                Assert.That(request.Headers.GetValues("X-API-Key").Single(), Is.EqualTo("test-key"));
                Assert.That(query["country"].ToString(), Is.EqualTo("de"));
                Assert.That(query["show_type"].ToString(), Is.EqualTo("movie"));
                Assert.That(query["year_min"].ToString(), Is.EqualTo("2020"));
                Assert.That(query["year_max"].ToString(), Is.EqualTo("2020"));
                Assert.That(query["genres"].ToString(), Is.EqualTo("drama"));
                Assert.That(query["order_by"].ToString(), Is.EqualTo("release_date"));
                Assert.That(query["order_direction"].ToString(), Is.EqualTo("asc"));
                Assert.That(query["cursor"].ToString(), Is.EqualTo("next?x=1&b=two+three"));
            });
            return Task.FromResult(Json("""{"shows":[],"hasMore":true,"nextCursor":"opaque+next"}"""));
        });
        var result = await Client(handler).SearchAsync(new MovieSearch
        {
            Year = 2020,
            Genre = "drama",
            SortBy = "year",
            Direction = "asc",
            Cursor = "next?x=1&b=two+three"
        }, default);
        Assert.That(result.NextCursor, Is.EqualTo("opaque+next"));
    }

    [Test]
    public async Task Title_search_filters_then_sorts_before_paging()
    {
        var shows = Enumerable.Range(1, 15).Select(i => new
        {
            id = i.ToString(),
            showType = "movie",
            title = $"Film {i}",
            originalTitle = $"Film {i}",
            releaseYear = i <= 12 ? 2020 : 2019,
            overview = "Plot",
            rating = i,
            genres = new[] { new { id = "drama", name = "Drama" } },
            imageSet = new { verticalPoster = new { w360 = "https://example.com/poster.jpg" } }
        });
        using var handler = new StubHandler((request, _) =>
        {
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/v4/shows/search/title"));
            Assert.That(QueryHelpers.ParseQuery(request.RequestUri.Query)["title"].ToString(), Is.EqualTo("Film & friends"));
            return Task.FromResult(Json(JsonSerializer.Serialize(shows)));
        });
        var search = new MovieSearch { Title = " Film & friends ", Year = 2020, Genre = "drama" };
        var client = Client(handler);
        var first = await client.SearchAsync(search, default);
        search.Cursor = first.NextCursor;
        var second = await client.SearchAsync(search, default);
        Assert.Multiple(() =>
        {
            Assert.That(first.Items.Select(m => m.Rating), Is.EqualTo(Enumerable.Range(3, 10).Reverse()));
            Assert.That(first.Items[0].PosterUrl, Is.EqualTo("https://example.com/poster.jpg"));
            Assert.That(second.Items.Select(m => m.Rating), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(second.HasMore, Is.False);
            Assert.That(second.NextCursor, Is.Null);
            Assert.That(first.Notice, Is.Not.Empty);
        });
    }

    [TestCase(429, 503)]
    [TestCase(401, 502)]
    [TestCase(500, 502)]
    public void Provider_errors_do_not_expose_response_body(int upstream, int expected)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Json("private-provider-data", (HttpStatusCode)upstream)));
        var error = Assert.ThrowsAsync<CatalogException>(() => Client(handler).GenresAsync(default));
        Assert.That(error!.StatusCode, Is.EqualTo(expected));
        Assert.That(error.Message, Does.Not.Contain("private-provider-data"));
    }

    [TestCase("not-json")]
    [TestCase("null")]
    [TestCase("{\"shows\":null,\"hasMore\":false}")]
    [TestCase("{\"shows\":[],\"hasMore\":true}")]
    public void Malformed_page_becomes_bad_gateway(string body)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Json(body)));
        var error = Assert.ThrowsAsync<CatalogException>(() => Client(handler).SearchAsync(new(), default));
        Assert.That(error!.StatusCode, Is.EqualTo(502));
    }

    [Test]
    public void Missing_key_does_not_contact_provider()
    {
        using var handler = new StubHandler((_, _) => throw new AssertionException("Unexpected provider request"));
        var error = Assert.ThrowsAsync<CatalogException>(() => Client(handler, "").GenresAsync(default));
        Assert.That(error!.StatusCode, Is.EqualTo(503));
    }

    [Test]
    public void Timeout_becomes_gateway_timeout()
    {
        using var handler = new StubHandler((_, _) => throw new TaskCanceledException());
        var error = Assert.ThrowsAsync<CatalogException>(() => Client(handler).GenresAsync(default));
        Assert.That(error!.StatusCode, Is.EqualTo(504));
    }

    [Test]
    public void Caller_cancellation_is_not_reported_as_provider_timeout()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new StubHandler((_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            throw new AssertionException("Expected cancellation");
        });
        Assert.CatchAsync<OperationCanceledException>(() => Client(handler).GenresAsync(cancellation.Token));
    }

    [TestCase("bad")]
    [TestCase("title:-10")]
    [TestCase("title:1")]
    public void Invalid_title_cursor_is_rejected_before_provider_call(string cursor)
    {
        using var handler = new StubHandler((_, _) => throw new AssertionException("Unexpected provider request"));
        var error = Assert.ThrowsAsync<CatalogException>(() => Client(handler).SearchAsync(new() { Title = "Film", Cursor = cursor }, default));
        Assert.That(error!.StatusCode, Is.EqualTo(400));
    }
}
