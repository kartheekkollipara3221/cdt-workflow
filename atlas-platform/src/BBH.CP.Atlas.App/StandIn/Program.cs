// STAND-IN: do not copy to the target.
// Local demo host. The target has its own program.cs; at the target, add only the platform lines
// that INTEGRATION.md lists (AddAtlasPlatform and one AddAtlasModule line for each app).
// C0: skeleton only. C6 adds Windows authentication, the database contexts, the platform and the controllers.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", host = "atlas-platform stand-in" }));
app.MapControllers();

app.Run();

public partial class Program;
