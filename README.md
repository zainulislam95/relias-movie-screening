# Movie Screening

A full-stack movie discovery application built with ASP.NET Core and Angular.

Users can search and filter movies, sort results, browse paginated results, and maintain a persistent favorites collection. Movie data is provided by the Streaming Availability API, while favorites are stored locally in SQLite.

## Features

- Search movies by title
- Filter by release year and genre
- Sort by title, year, or rating
- Paginated movie results
- Add movies to favorites
- View and remove favorites
- Persistent favorites using SQLite
- Responsive Angular UI
- Loading, empty, validation, and error states
- Swagger/OpenAPI documentation
- Backend unit and integration tests
- Frontend end-to-end tests with Playwright

## Tech stack

### Backend

- .NET 10 / ASP.NET Core Web API
- EF Core
- SQLite
- Typed `HttpClient`
- Problem Details for API errors
- Swagger / OpenAPI
- NUnit

### Frontend

- Angular 22
- TypeScript
- Angular Signals
- Reactive Forms
- Custom responsive CSS
- Playwright

## Architecture

The frontend communicates only with the ASP.NET Core backend. The backend is responsible for communicating with the Streaming Availability API, which keeps the provider API key out of browser code.

```text
Angular
   |
   v
ASP.NET Core API
   |
   +--> Streaming Availability API
   |
   +--> SQLite (Favorites)
```

Backend responsibilities are separated into controllers, services, provider clients, and persistence.

```text
Controllers
  FavoritesController -> IFavoriteService
  MoviesController / GenresController -> IMovieService

Services
  FavoriteService -> IFavoriteRepository + IMovieService
  MovieService -> IMovieCatalog

Persistence
  FavoriteRepository -> FavoritesDbContext -> SQLite

External integration
  StreamingAvailabilityClient -> Streaming Availability API
```

`MovieService` intentionally remains a small application boundary between controllers and the external catalog client. `FavoriteService` contains the favorites workflow and coordinates provider lookup with persistence.

## Requirements

- .NET 10 SDK
- Node.js supported by Angular 22:
  - Node 22.22.3+
  - Node 24.15.0+
  - Node 26.0.0+
- npm
- A free Streaming Availability API key

Create an API key at:

https://developers.movieofthenight.com/

## Quick start

### 1. Configure the API key

From the repository root:

```sh
dotnet user-secrets set "Streaming:ApiKey" "YOUR_API_KEY" --project backend/MovieScreening.Api
```

Alternatively, provide the key through the `Streaming__ApiKey` environment variable.

The default country is Germany (`de`). It can be overridden with:

```text
Streaming__Country
```

### 2. Start the backend

PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project backend/MovieScreening.Api --no-launch-profile -- --urls http://localhost:5080
```

The backend will be available at:

- API: `http://localhost:5080`
- Health check: `http://localhost:5080/health`
- Swagger: `http://localhost:5080/swagger`

Swagger is enabled only in the Development environment.

### 3. Start the frontend

In a second terminal:

```sh
cd frontend
npm ci
npm start
```

Open:

```text
http://127.0.0.1:4200
```

The Angular development server proxies `/api/**` requests to the backend on port `5080`, so the provider credentials never need to be exposed to the frontend.

## API

| Request | Purpose |
| --- | --- |
| `GET /api/movies` | Browse/search movies |
| `GET /api/genres` | Retrieve available genres |
| `GET /api/favorites` | Retrieve saved favorites |
| `GET /api/favorites/{movieId}` | Retrieve one favorite |
| `PUT /api/favorites/{movieId}` | Add a movie to favorites |
| `DELETE /api/favorites/{movieId}` | Remove a movie from favorites |

### Movie search

`GET /api/movies` accepts:

- `title`
- `year` (`1888`–`2100`)
- `genre`
- `sortBy` (`title`, `year`, `rating`)
- `direction` (`asc`, `desc`)
- `cursor`

The default ordering is rating descending.

Filtered browsing uses the provider's cursor-based pagination. Responses contain:

```text
items
hasMore
nextCursor
notice
```

When requesting the next page, the same filters and sorting should be used together with the returned cursor. If a filter or sort option changes, the cursor should be discarded.

Title search is handled differently because the provider returns a limited title-match result set. Matching results are filtered and sorted by the backend and paginated locally.

The frontend keeps previous pages for the current search in memory so navigating back does not require another provider call.

## Favorites

Favorites are persisted in SQLite.

The default database location is:

```text
backend/MovieScreening.Api/App_Data/favorites.db
```

The file is created automatically on startup and is ignored by Git.

A custom SQLite connection string can be provided using:

```text
ConnectionStrings__Favorites
```

Favorites use the numeric provider movie ID.

`PUT /api/favorites/{movieId}` is idempotent:

- `201 Created` when a movie is saved for the first time
- `204 No Content` when the movie is already saved
- `404 Not Found` when the provider movie does not exist
- `400 Bad Request` for an invalid ID

`DELETE /api/favorites/{movieId}` returns `204 No Content`, including repeated delete requests.

Each favorite stores:

- provider movie ID
- a JSON snapshot of the movie
- the time it was added

Keeping a snapshot allows previously saved favorites to remain readable if the external provider is temporarily unavailable.

The demo intentionally uses one shared favorites collection and does not implement user accounts.

## Error handling and external API behavior

API errors use RFC-style Problem Details responses and include a trace ID for diagnostics.

Typical responses include:

- `400` for invalid input
- `404` when a requested movie is not found
- `502` for provider failures
- `503` for unavailable provider configuration or quota-related failures
- `504` for provider timeouts
- `500` for unexpected application errors

The Streaming Availability API client has a 15-second timeout and propagates request cancellation.

Automatic retries are intentionally not enabled because the external API is quota-limited. A user can retry a failed operation explicitly rather than having the application multiply provider requests automatically.

## Frontend design

The Angular application uses standalone components and keeps application state intentionally lightweight.

- `MovieApiService` owns typed HTTP communication.
- `MovieStore` manages search results, pagination, cancellation, and favorites state using Angular signals.
- `MovieCardComponent` is a reusable presentation component and does not perform HTTP requests directly.
- Reactive Forms provide search validation.
- Stale search requests are cancelled when a newer search starts.
- Failed favorite operations do not optimistically leave the UI in an incorrect state.
- Loading, empty, failure, and missing-poster states are handled explicitly.
- Custom CSS provides responsive layouts without introducing a UI component library.

A router or larger global state framework was not added because the assignment contains only two lightweight views. Those would become reasonable additions if the application grew to include detail pages or more independent workflows.

## Tests

### Backend

Run all backend tests:

```sh
dotnet test backend/MovieScreening.Api.Tests
```

Run only unit tests:

```sh
dotnet test backend/MovieScreening.Api.Tests --filter Category=Unit
```

Run only integration tests:

```sh
dotnet test backend/MovieScreening.Api.Tests --filter Category=Integration
```

Backend tests cover controller behavior, services, repositories, HTTP contracts, SQLite persistence, and duplicate favorite handling.

Tests use isolated data and do not require Streaming Availability API credentials or consume provider quota.

### Frontend

From `frontend`:

```sh
npm ci
npx playwright install chromium
npm test
```

Playwright tests use deterministic mock API responses and cover search filters, pagination behavior, page caching, stale request cancellation, favorites, validation, failures/retries, and responsive layout.

Build the frontend with:

```sh
npm run build
```

The production output is written to:

```text
frontend/dist/movie-screening-ui/browser
```

## Design decisions

### Backend proxy for the external API

The Angular client does not call the Streaming Availability API directly. The backend owns that integration so API credentials remain server-side and the frontend is not coupled directly to the provider contract.

### SQLite for favorites

SQLite provides real persistence without requiring reviewers to install or configure a separate database server. It is appropriate for the scope of this local coding assignment.

### Idempotent favorites API

`PUT /api/favorites/{movieId}` represents the desired state that a known movie should be a favorite. Repeating the same request therefore has the same final result.

The SQLite primary key and `ON CONFLICT DO NOTHING` also make simultaneous duplicate additions safe.

### Simple application structure

The solution deliberately avoids CQRS, MediatR, microservices, and other infrastructure that would add complexity without providing value for this scope.

The architecture keeps responsibilities separate while remaining small enough to understand quickly.

### No automatic external API retries

Provider requests are not automatically retried because the external API has a usage quota. This avoids turning one failed user action into several provider calls.

## Trade-offs and possible improvements

For a production system, possible next steps would include:

- user authentication and per-user favorites
- EF Core migrations instead of `EnsureCreated`
- caching relatively static provider data such as genres
- production monitoring and distributed tracing
- deployment configuration
- a refresh strategy for saved movie snapshots
- routing and dedicated movie-detail pages if the frontend grows

The current implementation intentionally prioritizes a simple, reliable solution for the requested assignment rather than adding production infrastructure that the scope does not require.

## Repository hygiene

Provider credentials must be stored in .NET user secrets or environment variables.

Secrets, local databases, build output, editor settings, and local configuration files are excluded from source control.
