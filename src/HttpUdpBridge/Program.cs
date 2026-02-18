using HttpUdpBridge.Endpoints;
using HttpUdpBridge.Startup;

using var app = StartupHelpers.CreateApplication(args);

app.MapBridgeEndpoints();
app.MapGet("/hc", () => Results.Ok(new { status = "healthy" }));

await StartupHelpers.RunAppAsync(app).ConfigureAwait(false);
