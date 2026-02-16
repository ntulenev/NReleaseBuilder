using NReleaseBuilder.Application;

var app = new VersionCheckApplication();
return await app.RunAsync(CancellationToken.None).ConfigureAwait(false);
