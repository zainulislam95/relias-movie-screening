namespace MovieScreening.Api.Models;

public sealed class Favorite
{
    public string MovieId { get; set; } = "";
    public string MovieJson { get; set; } = "";
    public DateTime AddedAtUtc { get; set; }
}
