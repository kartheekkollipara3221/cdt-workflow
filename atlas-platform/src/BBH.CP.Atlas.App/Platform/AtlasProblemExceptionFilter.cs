using BBH.CP.Atlas.Service.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>Changes an <see cref="AtlasProblemException"/> to a problem response with its status (404, 400, 403).</summary>
public sealed class AtlasProblemExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AtlasProblemException problem)
            return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = problem.StatusCode,
            Title = problem.StatusCode switch
            {
                403 => "Access denied",
                404 => "Not found",
                _ => "Bad request"
            },
            Detail = problem.Message
        })
        {
            StatusCode = problem.StatusCode
        };
        context.ExceptionHandled = true;
    }
}
