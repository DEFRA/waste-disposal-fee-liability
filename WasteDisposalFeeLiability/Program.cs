using Microsoft.AspNetCore.HeaderPropagation;
using WasteDisposalFeeLiability.Billing.Clients;
using WasteDisposalFeeLiability.Billing.Config;
using WasteDisposalFeeLiability.Billing.Endpoints;
using WasteDisposalFeeLiability.Billing.Services;
using WasteDisposalFeeLiability.Organisations.Clients;
using WasteDisposalFeeLiability.Organisations.Config;
using WasteDisposalFeeLiability.Organisations.Endpoints;
using WasteDisposalFeeLiability.Utils;
using WasteDisposalFeeLiability.Utils.Http;
using WasteDisposalFeeLiability.Utils.HealthChecks;
using System.Diagnostics.CodeAnalysis;
using WasteDisposalFeeLiability.Utils.Logging;
using Serilog;

var app = BuildApp(args);
await app.RunAsync();

[ExcludeFromCodeCoverage]
static WebApplication BuildApp(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    ConfigureHost(builder);
    ConfigureServices(builder);

    var app = builder.Build();

    ConfigureMiddleware(app);
    ConfigureEndpoints(app);

    return app;
}

[ExcludeFromCodeCoverage]
static void ConfigureHost(WebApplicationBuilder builder)
{
    builder.Host.UseSerilog(CdpLogging.Configuration);
}

[ExcludeFromCodeCoverage]
static void ConfigureServices(WebApplicationBuilder builder)
{
    var services = builder.Services;
    var configuration = builder.Configuration;

    // Trust material must be loaded before anything creates outbound connections.
    services.LoadCustomTrustStoreFromEnvironment();

    services.AddProblemDetails();
    services.AddValidation();

    services.AddHttpContextAccessor();

    ConfigureHeaderPropagation(services, configuration);
    ConfigureHttpClients(services);
    ConfigureBilling(services, configuration);
    ConfigureOrganisations(services, configuration);

    services.AddHealthChecks();
}

[ExcludeFromCodeCoverage]
static void ConfigureHeaderPropagation(IServiceCollection services, IConfiguration configuration)
{
    var traceHeader = configuration.GetValue<string>("TraceHeader");

    services.AddHeaderPropagation(options =>
    {
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });
}

[ExcludeFromCodeCoverage]
static void ConfigureHttpClients(IServiceCollection services)
{
    services.AddTransient<ProxyHttpMessageHandler>();

    // NOTE: these calls carry no authentication for now. The original Azure implementation attached
    // an Entra ID bearer token acquired via Managed Identity - that mechanism doesn't exist on AWS/CDP,
    // and no replacement (IAM-based service-to-service auth, mTLS, etc) has been decided yet. Revisit
    // once calculator-api's own CDP migration settles on an inter-service auth story.
    services.AddHttpClientWithTracing<IEprCalculatorApiClient, EprCalculatorApiClient>();
    services.AddHttpClientWithTracing<IEprCommonDataApiClient, EprCommonDataApiClient>();
}

[ExcludeFromCodeCoverage]
static void ConfigureBilling(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddOptions<EprCalculatorApiSettings>()
        .Bind(configuration.GetRequiredSection(EprCalculatorApiSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddHostedService(serviceProvider => new CalculatorApiStartupProbe(
        serviceProvider.GetRequiredService<IEprCalculatorApiClient>(),
        serviceProvider.GetRequiredService<ILogger<CalculatorApiStartupProbe>>(),
        TimeSpan.FromSeconds(60),
        serviceProvider.GetRequiredService<HeaderPropagationValues>()));
}

[ExcludeFromCodeCoverage]
static void ConfigureOrganisations(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddOptions<EprCommonDataApiSettings>()
        .Bind(configuration.GetRequiredSection(EprCommonDataApiSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
}

[ExcludeFromCodeCoverage]
static void ConfigureMiddleware(WebApplication app)
{
    app.UseSerilogRequestLogging();

    app.UseHeaderPropagation();
}

[ExcludeFromCodeCoverage]
static void ConfigureEndpoints(WebApplication app)
{
    app.MapHealthChecks("/health", HealthCheckOptionsBuilder.Build());

    app.MapBillingEndpoints();
    app.MapOrganisationsEndpoints();
}
