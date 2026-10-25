// STAND-IN: do not copy to the target.
// Local demo host. The target has its own program.cs; at the target, add only the platform lines
// that INTEGRATION.md lists (AddAtlasPlatform, one AddAtlasModule line for each app, StartAtlasPlatformAsync).
using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.App.StandIn;

WebApplication app = StandInHost.Create(args);
await app.Services.StartAtlasPlatformAsync();
await app.RunAsync();

public partial class Program;
