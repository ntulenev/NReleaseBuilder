using System.Net.Http.Headers;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Application;
using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Csv;
using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Abstractions.Transport;
using NReleaseBuilder.Application;
using NReleaseBuilder.Bitbucket;
using NReleaseBuilder.Bitbucket.Internal;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Csv;
using NReleaseBuilder.Jira;
using NReleaseBuilder.Jira.Internal;
using NReleaseBuilder.Presentation;
using NReleaseBuilder.Presentation.Console;
using NReleaseBuilder.Presentation.Excel;
using NReleaseBuilder.Presentation.Pdf;
using NReleaseBuilder.Transport;

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
builder.Services.AddTransient<ICsvComponentNameFilterBuilder, CsvComponentNameFilterBuilder>();
builder.Services.AddTransient<ICsvComponentRowSourceReader, CsvComponentRowSourceReader>();
builder.Services.AddTransient<ICsvComponentRowsMerger, CsvComponentRowsMerger>();
builder.Services.AddTransient<ICsvImageParser, CsvImageParser>();
builder.Services.AddTransient<IHttpRetryExecutor, HttpRetryExecutor>();
builder.Services.AddTransient<IResponseSerializer, ResponseSerializer>();
builder.Services.AddTransient<IBitbucketIntegrationCore, BitbucketIntegrationCore>();
builder.Services.AddTransient<IBitbucketTagLookupCore, BitbucketTagLookupCore>();
builder.Services.AddTransient<IJiraParser, JiraParser>();
builder.Services.AddTransient<IJiraIntegrationCore, JiraIntegrationCore>();
builder.Services.AddTransient<IJiraTaskResolver, JiraTaskResolver>();
builder.Services.AddTransient<IBitbucketTagClient, BitbucketTagClient>();
builder.Services.AddTransient<IRepositoryNameNormalizer, RepositoryNameNormalizer>();
builder.Services.AddTransient<IRepositoryTagLookupBatchLoader, RepositoryTagLookupBatchLoader>();
builder.Services.AddTransient<IComponentVersionChecker, ComponentVersionChecker>();
builder.Services.AddTransient<IComponentsVersionBuilder, ComponentsVersionBuilder>();
builder.Services.AddTransient<IJiraStatusStatisticsConverter, JiraStatusStatisticsConverter>();
builder.Services.AddSingleton<IReportRunContextAccessor, ReportRunContextAccessor>();
builder.Services.AddTransient<IConsoleOutputRenderer, SpectreConsoleOutputRenderer>();
builder.Services.AddTransient<IExcelContentComposer, MiniExcelContentComposer>();
builder.Services.AddTransient<IExcelReportFileStore, ExcelReportFileStore>();
builder.Services.AddTransient<IWorkbookFormatter, OpenXmlWorkbookFormatter>();
builder.Services.AddTransient<IExcelReportRenderer, MiniExcelReportRenderer>();
builder.Services.AddTransient<IPdfContentComposer, PdfContentComposer>();
builder.Services.AddTransient<IPdfReportFileStore, PdfReportFileStore>();
builder.Services.AddTransient<IPdfReportRenderer, QuestPdfReportRenderer>();
builder.Services.AddTransient<IRenderer, GeneralFacadeRenderer>();
builder.Services.AddTransient<IVersionCheckApplication, VersionCheckApplication>();

using var host = builder.Build();
using var cancellationSource = new CancellationTokenSource();

ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};
Console.CancelKeyPress += cancelHandler;

try
{
    var app = host.Services.GetRequiredService<IVersionCheckApplication>();
    return await app.RunAsync(cancellationSource.Token).ConfigureAwait(false);
}
catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
{
    return 130;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}

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
