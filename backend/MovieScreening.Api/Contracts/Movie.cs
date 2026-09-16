namespace MovieScreening.Api.Contracts;

public record Genre(string Id, string Name);
public record Movie(string Id, string Title, string OriginalTitle, int? Year,
    string Overview, int? Rating, string? PosterUrl, Genre[] Genres);
public record MoviePage(Movie[] Items, bool HasMore, string? NextCursor, string? Notice = null);
