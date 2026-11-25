using System.Diagnostics;
using Babasketet.API.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Babasketet.API.Api;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainEx)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = domainEx.StatusCode,
            Title = domainEx.Code,
            Detail = domainEx.Message,
            Type = $"https://babasketet.app/errors/{domainEx.Code.ToLowerInvariant().Replace('_', '-')}",
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = domainEx.Code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = domainEx.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
