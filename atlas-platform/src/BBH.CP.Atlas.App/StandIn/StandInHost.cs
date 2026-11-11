// STAND-IN: do not copy to the target.
// The local demo host. Program.cs and the endpoint tests use it.
using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.App.UserAuthentication;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Infra;
using BBH.CP.Atlas.Service.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.App.StandIn;

/// <summary>Builds the local demo host with the platform lines, the stand-in authentication and the controllers.</summary>
public static class StandInHost
{
    /// <summary>The name of the CORS policy for the client origins in AppSettings:HostedClientURL.</summary>
    public const string CorsPolicy = "_corsOrigins";

    /// <summary>
    /// Creates the local web application.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="configure">An optional change to the builder before the services are added. The tests use it.</param>
    /// <exception cref="InvalidOperationException">
    /// The authentication is "Fixed" and the environment is not Development.
    /// </exception>
    public static WebApplication Create(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        IConfiguration configuration = builder.Configuration;

        builder.Services.AddDbContext<CPSharepointContext>(o => o.UseSqlServer(TPAMAPIAccess.GetConnectionString(configuration, "DBConnection")));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IUserInfo, UserInfo>();

        // Platform lines (the same lines go into the target program.cs).
        builder.Services.AddAtlasPlatform(configuration);
        builder.Services.AddAtlasModule<ConfigSectionService>("TTDV2");

        // Authentication: "Fixed" (default) = the Windows login of the API process; "Negotiate" = Windows authentication.
        if (string.Equals(configuration["StandIn:Authentication"], "Negotiate", StringComparison.OrdinalIgnoreCase))
            builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
        else if (!builder.Environment.IsDevelopment())
            // On a shared QC or demo machine, each caller would be the service account, with its roles.
            throw new InvalidOperationException(
                $"The stand-in Fixed authentication runs only in Development (this is {builder.Environment.EnvironmentName}). Set StandIn:Authentication = Negotiate.");
        else
            builder.Services.AddAuthentication(StandInFixedUserHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, StandInFixedUserHandler>(StandInFixedUserHandler.SchemeName, null);
        builder.Services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        builder.Services.AddControllers().AddApplicationPart(typeof(StandInHost).Assembly).AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
            policy.WithOrigins((configuration["AppSettings:HostedClientURL"] ?? "http://localhost:4200").Split(';', StringSplitOptions.RemoveEmptyEntries))
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        WebApplication app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CP Atlas V1 (stand-in)"));
        }

        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseMiddleware<StandInActAsMiddleware>();
        app.UseAuthorization();
        app.MapGet("/health", () => Results.Ok(new { status = "ok", host = "atlas-platform stand-in" })).AllowAnonymous();
        app.MapControllers();
        return app;
    }
}
