using MovieScreening.Api.Contracts;

namespace MovieScreening.Api.Integrations.StreamingAvailability;

internal sealed record ProviderPage(ProviderMovie[] Shows, bool HasMore, string? NextCursor);

internal sealed record ProviderMovie(string Id, string ShowType, string Title, string OriginalTitle,
    int? ReleaseYear, string Overview, int? Rating, ProviderImages? ImageSet, Genre[] Genres)
{
    public Movie ToMovie() => new(Id, Title, OriginalTitle, ReleaseYear, Overview, Rating,
        ImageSet?.VerticalPoster?.GetValueOrDefault("w360"), Genres);
}

internal sealed record ProviderImages(Dictionary<string, string>? VerticalPoster);
