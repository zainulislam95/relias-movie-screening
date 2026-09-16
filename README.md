# Movie Screening

ASP.NET Core and Angular application for discovering movies and saving favorites.

## Status

The repository currently contains the backend foundation. Movie provider integration,
favorites persistence, automated tests, and the Angular interface are the next increments.

## Run the backend

Install the .NET 10 SDK, then run from the repository root:

```sh
dotnet restore backend/MovieScreening.Api
dotnet build backend/MovieScreening.Api --no-restore
dotnet run --project backend/MovieScreening.Api --no-launch-profile -- --urls http://localhost:5080
```

Check `http://localhost:5080/health`. With `ASPNETCORE_ENVIRONMENT=Development`,
the OpenAPI document is available at `/openapi/v1.json`.

## Design decisions

- One repository keeps backend and frontend changes together for review.
- .NET 10 matches the installed SDK. One API project keeps the assignment small;
  contracts and services separate responsibilities without extra architectural layers.
- A typed HTTP client will isolate Streaming Availability API details. Angular will
  call our backend so the provider key stays on the server.
- SQLite with EF Core will persist favorites without requiring a database server.
- The provider uses cursor pagination for filtered browsing. Title search instead
  returns a limited set of matches without pagination. Those matches will be
  filtered by year and genre, sorted, and paginated locally. This does not represent
  an exhaustive search of the entire provider catalog.

## Configuration and repository hygiene

Use .NET user secrets or `Streaming__ApiKey` for the API key when provider integration
is added. Never put credentials in source-controlled settings or Angular code.
Build output, local databases, editor settings, and local secret files are ignored.

## Planned review increments

1. Backend foundation and repository configuration.
2. Movie search integration, request validation, error handling, and contract tests.
3. Persistent, idempotent favorites and database integration tests.
4. Angular search and favorites interface, including loading, empty, and error states.

Provider documentation: https://docs.movieofthenight.com/
