using System.ComponentModel.DataAnnotations;

namespace MovieScreening.Api.Contracts;

public sealed class MovieSearch
{
    [StringLength(200)] public string? Title { get; set; }
    [Range(1888, 2100)] public int? Year { get; set; }
    [RegularExpression("^[a-z]{1,30}$")] public string? Genre { get; set; }
    [Required, RegularExpression("^(title|year|rating)$")] public string SortBy { get; set; } = "rating";
    [Required, RegularExpression("^(asc|desc)$")] public string Direction { get; set; } = "desc";
    [StringLength(2048)] public string? Cursor { get; set; }
}
