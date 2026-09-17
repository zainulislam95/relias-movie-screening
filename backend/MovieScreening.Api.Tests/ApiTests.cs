using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Microsoft.Data.Sqlite;

namespace MovieScreening.Api.Tests;

public sealed class ApiTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new($"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared");

        public Factory() => connection.Open();

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await connection.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Streaming:ApiKey"] = "", ["Streaming:Country"] = "de",
                    ["ConnectionStrings:Favorites"] = connection.ConnectionString
                }));
    }

    [TestCase("year=1700")]
    [TestCase("sortBy=unsupported")]
    [TestCase("direction=sideways")]
    [TestCase("genre=invalid%26genre")]
    public async Task Invalid_search_is_a_validation_problem(string query)
    {
        await using var app = new Factory();
        using var client = app.CreateClient();
        using var response = await client.GetAsync($"/api/movies?{query}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem!.Errors, Is.Not.Empty);
    }

    [TestCase("/api/movies")]
    [TestCase("/api/genres")]
    public async Task Missing_configuration_returns_safe_problem(string path)
    {
        await using var app = new Factory();
        using var client = app.CreateClient();
        using var response = await client.GetAsync(path);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(problem!.Status, Is.EqualTo(503));
            Assert.That(problem.Extensions.ContainsKey("traceId"), Is.True);
        });
    }

    [Test]
    public async Task Openapi_includes_movie_and_genre_routes()
    {
        await using var app = new Factory();
        using var client = app.CreateClient();
        var document = await client.GetStringAsync("/openapi/v1.json");
        Assert.That(document, Does.Contain("/api/movies").And.Contain("/api/genres"));
    }
}
