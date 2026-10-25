// STAND-IN: do not copy to the target.
// Local demo host. The target has its own program.cs; at the target, add only the platform lines
// that INTEGRATION.md lists (AddAtlasPlatform, one AddAtlasModule line for each app, StartAtlasPlatformAsync,
// and the --atlas-check lines).
using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.App.StandIn;

bool check = args.Contains("--atlas-check", StringComparer.OrdinalIgnoreCase);
WebApplication app = StandInHost.Create([.. args.Where(x => !string.Equals(x, "--atlas-check", StringComparison.OrdinalIgnoreCase))]);
if (check)
    return await app.Services.RunAtlasCheckAsync(Console.Out);

await app.Services.StartAtlasPlatformAsync();
await app.RunAsync();
return 0;

public partial class Program;
