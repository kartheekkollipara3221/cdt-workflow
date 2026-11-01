using BBH.CP.Atlas.Service.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Changes an <see cref="AtlasProblemException"/> to a problem response with its status (404, 400, 403; 500 for a config
/// defect). The body comes from the ProblemDetailsFactory of the host (review SV-11), so it has the same type and
/// traceId as the other errors of the target (AddProblemDetails). Title, status and detail do not change.
/// </summary>
public sealed class AtlasProblemExceptionFilter(ILogger<AtlasProblemExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AtlasProblemException problem)
            return;

        if (problem.IsConfigError)
            AtlasLog.ConfigError(logger, problem, context.HttpContext.Request.Path.ToString(), problem.Message);

        string title = problem.StatusCode switch
        {
            403 => "Access denied",
            404 => "Not found",
            500 => "Configuration error",
            _ => "Bad request"
        };
        ProblemDetailsFactory? factory = context.HttpContext.RequestServices?.GetService<ProblemDetailsFactory>();
        ProblemDetails details = factory?.CreateProblemDetails(context.HttpContext, problem.StatusCode, title, detail: problem.Message)
            ?? new ProblemDetails { Status = problem.StatusCode, Title = title, Detail = problem.Message };

        context.Result = new ObjectResult(details) { StatusCode = problem.StatusCode };
        context.ExceptionHandled = true;
    }
}
