using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MovieScreening.Api.Exceptions;

namespace MovieScreening.Api.ErrorHandling;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problems,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var catalogException = exception as CatalogException;

        var status = catalogException?.StatusCode
                     ?? StatusCodes.Status500InternalServerError;

        if (catalogException is null)
        {
            logger.LogError(
                exception,
                "Unhandled request failure. TraceId: {TraceId}",
                context.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                exception,
                "Catalog request failed with status {StatusCode}. TraceId: {TraceId}",
                status,
                context.TraceIdentifier);
        }

        context.Response.StatusCode = status;

        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = catalogException?.Message
                        ?? "An unexpected error occurred.",
                Instance = context.Request.Path,
                Extensions =
                {
                    ["traceId"] = context.TraceIdentifier
                }
            }
        });
    }
}