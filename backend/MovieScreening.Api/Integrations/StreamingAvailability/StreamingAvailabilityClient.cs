using MovieScreening.Api.Configuration;
using MovieScreening.Api.Abstractions;
using MovieScreening.Api.Exceptions;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Integrations.StreamingAvailability;

public sealed class StreamingAvailabilityClient(HttpClient http, IOptions<StreamingOptions> options) : IMovieCatalog
{
    private const int TitlePageSize = 10;

    public async Task<MoviePage> SearchAsync(MovieSearch search, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["country"] = options.Value.Country,
            ["show_type"] = "movie",
            ["output_language"] = "en"
        };

        if (!string.IsNullOrWhiteSpace(search.Title))
            return await SearchTitleAsync(search, query, cancellationToken);

        query["year_min"] = search.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        query["year_max"] = query["year_min"];
        query["genres"] = search.Genre;
        query["order_by"] = search.SortBy switch
        {
            "title" => "original_title", "year" => "release_date", _ => "rating"
        };
        query["order_direction"] = search.Direction;
        query["cursor"] = search.Cursor;
        var page = await ReadAsync<ProviderPage>(QueryHelpers.AddQueryString("shows/search/filters", query), cancellationToken);
        if (page.Shows is null || (page.HasMore && string.IsNullOrWhiteSpace(page.NextCursor)))
            throw InvalidResponse();

        return new(MapMovies(page.Shows), page.HasMore, page.HasMore ? page.NextCursor : null);
    }

    private async Task<MoviePage> SearchTitleAsync(MovieSearch search, Dictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        if (search.Cursor is not null && (!search.Cursor.StartsWith("title:", StringComparison.Ordinal) ||
            !int.TryParse(search.Cursor[6..], out offset) || offset < 0 || offset > 10000 || offset % TitlePageSize != 0))
            throw new CatalogException(400, "Invalid title search cursor. Start a new search.");

        query["title"] = search.Title!.Trim();
        var shows = await ReadAsync<ProviderMovie[]>(QueryHelpers.AddQueryString("shows/search/title", query), cancellationToken);
        var movies = MapMovies(shows)
            .Where(m => search.Year is null || m.Year == search.Year)
            .Where(m => string.IsNullOrWhiteSpace(search.Genre) || m.Genres.Any(g => g.Id == search.Genre));
        var sorted = Sort(movies, search).ToArray();
        var hasMore = sorted.Length > offset + TitlePageSize;
        return new(sorted.Skip(offset).Take(TitlePageSize).ToArray(), hasMore,
            hasMore ? $"title:{offset + TitlePageSize}" : null,
            "Title search shows the provider's best matches (usually up to 20). Year and genre refine those matches.");
    }

    private static IOrderedEnumerable<Movie> Sort(IEnumerable<Movie> movies, MovieSearch search)
    {
        var descending = search.Direction == "desc";
        IOrderedEnumerable<Movie> sorted = search.SortBy switch
        {
            "title" => descending ? movies.OrderByDescending(m => m.OriginalTitle, StringComparer.OrdinalIgnoreCase)
                : movies.OrderBy(m => m.OriginalTitle, StringComparer.OrdinalIgnoreCase),
            "year" => descending ? movies.OrderByDescending(m => m.Year) : movies.OrderBy(m => m.Year),
            _ => descending ? movies.OrderByDescending(m => m.Rating) : movies.OrderBy(m => m.Rating)
        };
        return sorted.ThenBy(m => m.Id, StringComparer.Ordinal);
    }

    public Task<Genre[]> GenresAsync(CancellationToken cancellationToken) =>
        ReadAsync<Genre[]>("genres?output_language=en", cancellationToken);

    public async Task<Movie?> GetAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var path = QueryHelpers.AddQueryString($"shows/{Uri.EscapeDataString(id)}", "country", options.Value.Country);
            var show = await ReadAsync<ProviderMovie>(path, cancellationToken);
            return MapMovies([show]).SingleOrDefault();
        }
        catch (CatalogException ex) when (ex.StatusCode == 404) { return null; }
    }

    private static Movie[] MapMovies(ProviderMovie[] shows)
    {
        if (shows.Any(s => s is null || string.IsNullOrWhiteSpace(s.Id) ||
            string.IsNullOrWhiteSpace(s.Title) || s.Genres is null || string.IsNullOrWhiteSpace(s.ShowType)))
            throw InvalidResponse();
        return shows.Where(s => s.ShowType == "movie").Select(s => s.ToMovie()).ToArray();
    }

    private static CatalogException InvalidResponse() => new(502, "The movie provider returned an unreadable response. Please retry.");

    private async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new CatalogException(503, "Movie search is not configured. Set Streaming__ApiKey on the backend.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-API-Key", options.Value.ApiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw response.StatusCode switch
                {
                    HttpStatusCode.NotFound => new CatalogException(404, "Movie not found."),
                    HttpStatusCode.TooManyRequests => new CatalogException(503, "The movie provider's request limit was reached. Please try later."),
                    _ => new CatalogException(502, "The movie provider is unavailable. Please try again later.")
                };
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw InvalidResponse();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new CatalogException(504, "The movie provider took too long to respond. Please retry."); }
        catch (HttpRequestException)
        { throw new CatalogException(502, "The movie provider could not be reached. Please retry."); }
        catch (JsonException)
        { throw InvalidResponse(); }
    }
}
