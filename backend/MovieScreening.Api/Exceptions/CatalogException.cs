namespace MovieScreening.Api.Exceptions;

public sealed class CatalogException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
