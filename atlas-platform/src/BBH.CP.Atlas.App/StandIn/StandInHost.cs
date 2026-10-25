// STAND-IN: do not copy to the target.
// The local demo host (spec 12, 11.3). Program.cs and the endpoint tests use it.
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

public static class StandInHost
{
    public const string CorsPolicy = "_corsOrigins";

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
        builder.Services.AddAtlasModule<CPTTDV2Service>("TTDV2");

        // Authentication: "Fixed" (default) = the Windows login of the API process; "Negotiate" = Windows authentication.
        if (string.Equals(configuration["StandIn:Authentication"], "Negotiate", StringComparison.OrdinalIgnoreCase))
            builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
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
