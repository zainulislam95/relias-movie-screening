# Movie Screening

ASP.NET Core and Angular application for discovering movies and saving favorites.

## Status

The backend supports movie search and genre discovery through Streaming Availability
API v4, request validation, and consistent error responses. Automated tests use a
fake provider; they do not require credentials or consume API quota. Favorites
are persisted in SQLite. The Angular interface supports discovery, filters, sorting,
cursor pagination, and a shared favorites collection.

## Run the full application

Requirements: .NET 10 SDK and Node.js 24.15 or newer in the Node 24 release line
(with npm). Configure the provider key as described below. Open two terminals
in the repository folder.

Backend (PowerShell):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project backend/MovieScreening.Api --no-launch-profile -- --urls http://localhost:5080
```

Frontend:

```sh
cd frontend
npm ci
npm start
```

Open `http://127.0.0.1:4200`. The Angular development server forwards `/api/**`
to the backend on port 5080. The browser only talks to the frontend origin, so
the local demo needs no permissive CORS policy. Provider credentials never belong
in frontend configuration. Swagger remains at `http://localhost:5080/swagger`.

Search runs on form submission or a sort change rather than on every keystroke.
Changing filters starts a new page sequence. Previous pages are kept in memory
for the current search, avoiding repeat provider calls. Favorites are loaded
independently, and their saved state changes only after a successful server response.

### Frontend architecture and verification

- Angular standalone components keep application setup small. The root component
  owns the filter form and the Discover/Favorites view selection.
- `MovieApiService` owns typed HTTP calls. `MovieStore` owns results, request
  cancellation, pagination, and favorites state using Angular signals.
- `MovieCardComponent` is a reusable presentation component with explicit inputs
  and a favorite-toggle output. It does not make HTTP requests.
- Reactive forms validate search input. Loading, error, empty, and missing-poster
  states are handled explicitly. A failed favorite operation leaves the UI unchanged.
- The two views share one screen shell; a router and global state framework are
  unnecessary for this scope. A larger app with movie detail URLs would add routing.
- Custom CSS supplies the responsive layout without a UI component library. Fonts
  have local fallbacks, and the interface respects reduced-motion preferences.

From `frontend`:

```sh
npm run build
npx playwright install chromium
npm test
```

The browser tests use deterministic mock API responses and never consume the
provider's quota. They cover filters, cursor handling, page caching, stale request
cancellation, favorites, failures/retries, validation, and mobile layout. Backend
tests separately verify the real SQLite persistence and HTTP contracts.
To use an installed Edge browser instead of downloading Chromium, set
`PLAYWRIGHT_CHANNEL=msedge` (PowerShell: `$env:PLAYWRIGHT_CHANNEL = "msedge"`).

The production build is written to `frontend/dist/movie-screening-ui/browser`.
Deployment is not configured: a host would need to serve these static files and
route `/api` to the backend under the same origin. The current shared favorites
collection is intended for a local demo, not a public multi-user service.

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
| `GET /api/favorites` | Saved movies, newest first |
| `GET /api/favorites/{movieId}` | Retrieve one saved movie |
| `PUT /api/favorites/{movieId}` | Save a movie using its numeric provider ID |
| `DELETE /api/favorites/{movieId}` | Remove a saved movie |

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

### Favorites

Use the `id` returned by movie search (not `imdbId` or `tmdbId`). PUT returns 201
with a Location header for a new favorite, or 204 if already saved. DELETE returns
204 without a response body, including repeated requests. An unknown movie
returns 404 when adding. Invalid IDs return 400. Saving a new favorite needs provider
access; listing, deleting, or adding an already-saved favorite does not.

The default database is `backend/MovieScreening.Api/App_Data/favorites.db`, anchored
to the API content root rather than the terminal's working directory. It is created
on startup and ignored by Git. `ConnectionStrings__Favorites` can override the
SQLite connection string; for a custom file location, create its parent directory first.

The demo has one shared favorites collection, without accounts. Each row stores
the provider ID, a JSON snapshot of the movie details, and the time it was added.
This keeps saved movies readable during provider outages; details are not refreshed
automatically. JSON suits the current requirement because we display complete saved
movies without querying individual metadata fields.

The service uses EF Core directly, with no generic repository wrapper. A primary
key and a parameterized `ON CONFLICT DO NOTHING` insert make simultaneous additions
safe. PUT expresses the idempotent operation of making a known movie a favorite.

Startup uses `EnsureCreated` for this initial, single-table database. It does not
upgrade an existing schema. Introduce EF migrations before evolving the schema
with retained user data; this is not a production migration strategy.

To verify in Swagger: save a search result, list favorites, restart the server and
list again, then delete the movie. The tests also perform these checks against
isolated SQLite files and exercise simultaneous duplicate additions.

## Design decisions

- One repository keeps backend and frontend changes together for review.
- .NET 10 matches the installed SDK. One API project keeps the assignment small;
  contracts and services separate responsibilities without extra architectural layers.
- A typed HTTP client isolates Streaming Availability API details. Angular will
  call our backend so the provider key stays on the server.
- SQLite with EF Core persists favorites without requiring a database server.
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
3. Persistent, idempotent favorites and database integration tests — implemented.
4. Angular search and favorites interface, including loading, empty, and error states — implemented.

Provider documentation: https://docs.movieofthenight.com/
