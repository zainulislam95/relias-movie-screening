using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieScreening.Api.Data;
using MovieScreening.Api.Repositories;
using MovieScreening.Api.Contracts;
using NUnit.Framework;

namespace MovieScreening.Api.Tests;

[Category("Unit")]
public sealed class FavoriteRepositoryTests
{
    private static Movie Sample => new("123", "A Movie", "A Movie", 2020, "Plot", 85, null, new[] { new Genre("drama", "Drama") });

    private static FavoritesDbContext CreateContext(DbConnection connection)
    {
        var options = new DbContextOptionsBuilder<FavoritesDbContext>().UseSqlite(connection).Options;
        return new FavoritesDbContext(options);
    }

    [Test]
    public async Task TryAdd_and_query_and_remove_behave_as_expected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var repo = new FavoriteRepository(context);
        await repo.InitializeAsync(CancellationToken.None);

        var added = await repo.TryAddAsync(Sample, CancellationToken.None);
        Assert.That(added, Is.True);

        var exists = await repo.ExistsAsync(Sample.Id, CancellationToken.None);
        Assert.That(exists, Is.True);

        var byId = await repo.GetByIdAsync(Sample.Id, CancellationToken.None);
        Assert.That(byId, Is.Not.Null);
        Assert.That(byId!.Title, Is.EqualTo(Sample.Title));

        var all = await repo.GetAllAsync(CancellationToken.None);
        Assert.That(all.Select(m => m.Id), Is.EquivalentTo(new[] { Sample.Id }));

        // second insert should not create a new row
        var second = await repo.TryAddAsync(Sample, CancellationToken.None);
        Assert.That(second, Is.False);

        await repo.RemoveAsync(Sample.Id, CancellationToken.None);
        Assert.That(await repo.ExistsAsync(Sample.Id, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task Deserialize_throws_for_bad_json_in_storage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        // insert invalid json directly
        var now = DateTime.UtcNow;
        // Use interpolated parameters so values are passed as SQL parameters (and properly quoted)
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Favorites (MovieId, MovieJson, AddedAtUtc) VALUES ({"bad"}, {"not-json"}, {now})");
        var repo = new FavoriteRepository(context);
        Assert.ThrowsAsync<InvalidOperationException>(() => repo.GetByIdAsync("bad", CancellationToken.None));
    }
}
