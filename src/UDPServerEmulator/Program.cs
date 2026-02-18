using UDPServerEmulator.Startup;

using var app = StartupHelpers.CreateApplication(args);
await StartupHelpers.RunAppAsync(app).ConfigureAwait(false);
