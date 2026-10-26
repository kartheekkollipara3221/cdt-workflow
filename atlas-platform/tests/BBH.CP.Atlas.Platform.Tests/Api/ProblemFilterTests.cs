using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.Service.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace BBH.CP.Atlas.Platform.Tests.Api;

/// <summary>Review SV-11: a config defect is a logged 500, not a 400.</summary>
public class ProblemFilterTests
{
    [Fact]
    public void A_config_defect_gives_500_and_Atlas_ConfigError()
    {
        ListLogger<AtlasProblemExceptionFilter> logger = new();
        ExceptionContext context = new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [])
        {
            Exception = AtlasProblemException.Config("Option provider 'X' is not registered (app TTDV2).")
        };

        new AtlasProblemExceptionFilter(logger).OnException(context);

        ObjectResult result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(500, result.StatusCode);
        ProblemDetails details = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Configuration error", details.Title);
        Assert.Contains(logger.Entries, x => x.EventId.Name == "Atlas.ConfigError");
    }

    [Fact]
    public void A_request_error_keeps_its_status_and_is_not_logged_as_a_config_error()
    {
        ListLogger<AtlasProblemExceptionFilter> logger = new();
        ExceptionContext context = new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [])
        {
            Exception = AtlasProblemException.BadRequest("App 'TTDV2' has no SoftDelete setting.")
        };

        new AtlasProblemExceptionFilter(logger).OnException(context);

        Assert.Equal(400, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        Assert.Empty(logger.Entries);
    }
}
