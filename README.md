# Movie Screening

ASP.NET Core and Angular application for discovering movies and saving favorites.

## Status

The backend supports movie search and genre discovery through Streaming Availability
API v4, request validation, and consistent error responses. Automated tests use a
fake provider; they do not require credentials or consume API quota. Favorites
persistence and the Angular interface are the next increments.

## Run the backend

Install the .NET 10 SDK, then run from the repository root:

```sh
dotnet restore backend/MovieScreening.Api
dotnet build backend/MovieScreening.Api --no-restore
dotnet test backend/MovieScreening.Api.Tests
dotnet run --project backend/MovieScreening.Api --no-launch-profile -- --urls http://localhost:5080
```

Check `http://localhost:5080/health`. With `ASPNETCORE_ENVIRONMENT=Development`,
interactive Swagger UI is available at `http://localhost:5080/swagger`, and the
OpenAPI document is available at `/openapi/v1.json`. Both are disabled outside
Development. In Swagger, expand an endpoint, choose **Try it out**, enter parameters,
and choose **Execute** to inspect the response. The provider API key remains in
backend configuration; do not enter it in Swagger.

### Configure movie search

Create a free API key at https://developers.movieofthenight.com/. This integration
uses that platform's `X-API-Key` authentication, not RapidAPI credentials.
Store the key locally from the repository root:

```sh
dotnet user-secrets set "Streaming:ApiKey" "YOUR_API_KEY" --project backend/MovieScreening.Api
```

User secrets load in Development. In PowerShell, set the environment before starting:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project backend/MovieScreening.Api --no-launch-profile -- --urls http://localhost:5080
```

Alternatively, supply `Streaming__ApiKey` as a backend environment variable.
The default country is Germany (`de`); `Streaming__Country` can override it with a
supported lowercase country code. Restart after changing configuration.

### Endpoints

| Request | Purpose |
| --- | --- |
| `GET /api/movies` | Browse movies available in the configured country |
| `GET /api/movies?title=Batman&year=2008&genre=action` | Refine the provider's title matches |
| `GET /api/movies?genre=drama&sortBy=year&direction=desc` | Browse by genre and release date |
| `GET /api/genres` | Available genre IDs and names |

Search accepts `title`, `year` (1888–2100), `genre`, `sortBy` (`title`, `year`,
`rating`), `direction` (`asc`, `desc`), and `cursor`. The default sort is rating
descending. Title ordering uses the original title, matching the provider's browse
ordering; year ordering uses the full release date for browsing and the available
release year for title matches.

Responses contain `items`, `hasMore`, `nextCursor`, and an optional `notice`.
For the next page, URL-encode `nextCursor` and repeat the same filters and sorting.
Discard the cursor when any filter or sort changes. Browsing uses provider pages
of up to 20 movies; title matches are paginated locally in groups of 10. Title
search can include movies unavailable in the configured country. Each page request
currently calls the provider, so avoid automatic repeated searches on every keystroke.

Errors use Problem Details JSON: 400 for invalid input, 503 for missing configuration
or provider quota exhaustion, 502 for other provider failures, and 504 for timeouts.
The API uses a 15-second timeout and passes request cancellation through to the
provider. It does not automatically retry requests, to avoid consuming extra quota.

## Design decisions

- One repository keeps backend and frontend changes together for review.
- .NET 10 matches the installed SDK. One API project keeps the assignment small;
  contracts and services separate responsibilities without extra architectural layers.
- A typed HTTP client isolates Streaming Availability API details. Angular will
  call our backend so the provider key stays on the server.
- SQLite with EF Core will persist favorites without requiring a database server.
- The provider uses cursor pagination for filtered browsing. Title search instead
  returns a limited set of matches without pagination. Those matches are
  filtered by year and genre, sorted, and paginated locally. This does not represent
  an exhaustive search of the entire provider catalog.

## Configuration and repository hygiene

Use .NET user secrets or `Streaming__ApiKey` for the API key.
Never put credentials in source-controlled settings or Angular code.
Build output, local databases, editor settings, and local secret files are ignored.

## Review increments

1. Backend foundation and repository configuration — complete.
2. Movie search integration, request validation, error handling, and contract tests — implemented.
3. Persistent, idempotent favorites and database integration tests — planned.
4. Angular search and favorites interface, including loading, empty, and error states — planned.

Provider documentation: https://docs.movieofthenight.com/
