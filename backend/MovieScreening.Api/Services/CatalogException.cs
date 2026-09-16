namespace MovieScreening.Api.Services;

public sealed class CatalogException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
