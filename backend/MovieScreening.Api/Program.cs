using MovieScreening.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieScreening.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOptions<StreamingOptions>()
    .Bind(builder.Configuration.GetSection("Streaming"))
    .Validate(options => System.Text.RegularExpressions.Regex.IsMatch(options.Country, "^[a-z]{2}$"),
        "Streaming:Country must be a lowercase two-letter country code.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IMovieCatalog, StreamingAvailabilityClient>(client =>
{
    client.BaseAddress = new Uri("https://api.movieofthenight.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddDbContext<FavoritesDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Favorites");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        var directory = Path.Combine(services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "favorites.db")
        }.ToString();
    }
    options.UseSqlite(connectionString);
});
builder.Services.AddScoped<FavoriteService>();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<FavoritesDbContext>().Database.EnsureCreatedAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("../openapi/v1.json", "Movie Screening API v1");
        options.DocumentTitle = "Movie Screening API";
    });
}
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
