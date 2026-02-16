using System.Net.Http.Headers;
using System.Text;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Application;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Presentation;
using NReleaseBuilder.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Extensions.Http.DefaultHttpClientFactory", LogLevel.Warning);

builder.Services
    .AddOptions<AppSettings>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<AppSettings>, AppSettingsValidator>();

builder.Services.AddHttpClient(HttpClientNames.BITBUCKET, (sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<AppSettings>>().Value.Bitbucket;
    http.BaseAddress = EnsureTrailingSlash(options.BaseUrl);
    http.DefaultRequestHeaders.Authorization =
        BuildAuthHeader(options.AuthEmail, options.AuthApiToken);
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient(HttpClientNames.JIRA, (sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<AppSettings>>().Value.Jira;
    http.BaseAddress = EnsureTrailingSlash(options.BaseUrl);
    http.DefaultRequestHeaders.Authorization =
        BuildAuthHeader(options.ResolveAuthEmail(), options.ResolveAuthApiToken());
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddTransient<ICsvComponentReader, CsvComponentReader>();
builder.Services.AddTransient<IBitbucketTagClient, BitbucketTagClient>();
builder.Services.AddTransient<IComponentVersionChecker, ComponentVersionChecker>();
builder.Services.AddTransient<IJiraStatusStatisticsBuilder, JiraStatusStatisticsBuilder>();
builder.Services.AddTransient<IConsoleRenderer, SpectreConsoleRenderer>();
builder.Services.AddTransient<IVersionCheckApplication, VersionCheckApplication>();

using var host = builder.Build();

var app = host.Services.GetRequiredService<IVersionCheckApplication>();
return await app.RunAsync(CancellationToken.None).ConfigureAwait(false);

static AuthenticationHeaderValue BuildAuthHeader(string authEmail, string authApiToken)
{
    ArgumentNullException.ThrowIfNull(authEmail);
    ArgumentNullException.ThrowIfNull(authApiToken);

    var authRaw = $"{authEmail}:{authApiToken}";
    var authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authRaw));
    return new AuthenticationHeaderValue("Basic", authBase64);
}

static Uri EnsureTrailingSlash(Uri baseUrl)
{
    ArgumentNullException.ThrowIfNull(baseUrl);

    var normalized = baseUrl.ToString().TrimEnd('/') + "/";
    return new Uri(normalized, UriKind.Absolute);
}
