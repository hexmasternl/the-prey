using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using Microsoft.FeatureManagement;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // Every module names its ActivitySource and Meter HexMaster.ThePrey.{Module}; registering the
    // prefix here exports them all, so no API registers its own source or meter.
    private const string ProjectTelemetryPrefix = "HexMaster.ThePrey.*";

    // Standard OpenTelemetry setting for the service (role) name. Set per container app in Azure and
    // per resource by the Aspire AppHost; read by the OpenTelemetry SDK itself.
    private const string ServiceNameVariable = "OTEL_SERVICE_NAME";

    public const string DefaultCorsPolicyName = "DefaultCors";

    // Origins allowed when "Cors:AllowedOrigins" is not configured. Includes the Capacitor
    // WebView origins, which are constant across environments (the native app always reports
    // origin https://localhost on Android / capacitor://localhost on iOS regardless of which
    // API environment it targets), so they must be allowed in production as well as dev.
    private static readonly string[] DefaultCorsOrigins =
    [
        "http://localhost:8100",  // Ionic dev server (browser / `ionic serve`)
        "https://localhost",      // Capacitor Android WebView
        "capacitor://localhost",  // Capacitor iOS WebView
    ];

    // Auth0 tenant authority and API identifier (audience) shared by every backend service.
    // Override per environment via configuration keys "Auth0:Domain" and "Auth0:Audience".
    private const string DefaultAuthority = "https://theprey.eu.auth0.com/";
    private const string DefaultAudience = "https://api.theprey.nl";

    // Name of the environment variable that, when present, points at the Azure App Configuration
    // store to load connection values and settings from. Set in the cloud container apps; absent
    // locally (where configuration comes from the Aspire AppHost / user secrets).
    private const string AppConfigurationHostnameVariable = "AppConfigurationHostname";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddAzureAppConfiguration();

        // Register IFeatureManager. Must be called unconditionally so that IFeatureManager resolves
        // even locally where App Config is absent — flags simply evaluate to false (default-off).
        builder.Services.AddFeatureManagement();

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        builder.Services.AddDaprClient();

        return builder;
    }

    /// <summary>
    /// Adds Azure App Configuration as a configuration source when the
    /// <c>AppConfigurationHostname</c> environment variable is set (cloud environments). Connection
    /// values such as the Web PubSub and Service Bus endpoints live in the store; secrets are stored
    /// in Key Vault and resolved through Key Vault references using the app's managed identity.
    /// Does nothing locally, where the env var is absent and config comes from the Aspire AppHost.
    /// </summary>
    public static TBuilder AddAzureAppConfiguration<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var hostname = builder.Configuration[AppConfigurationHostnameVariable];
        if (string.IsNullOrWhiteSpace(hostname))
            return builder;

        var endpoint = hostname.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? hostname
            : $"https://{hostname}";

        var credential = new global::Azure.Identity.DefaultAzureCredential();

        builder.Configuration.AddAzureAppConfiguration(options =>
        {
            options
                .Connect(new Uri(endpoint), credential)
                .Select(KeyFilter.Any, LabelFilter.Null)
                .ConfigureKeyVault(keyVault => keyVault.SetCredential(credential))
                // Surface .appconfig.featureflag/* keys to IFeatureManager.
                // The 15-minute refresh interval means a flag flipped in Azure takes effect
                // within ~15 minutes without a container restart. Refresh is pull-based: the
                // UseAzureAppConfiguration middleware (wired via UseAppConfigurationRefresh)
                // triggers a (non-blocking) refresh on incoming requests, honoring this interval.
                .UseFeatureFlags(ff => ff.SetRefreshInterval(TimeSpan.FromMinutes(15)))
                // Key-values (e.g. Games:MinimumAppVersion) refresh every 5 minutes through the same
                // middleware, so values read from IConfiguration per request change without a restart.
                // Options bound once at startup (IOptions<T> rather than IOptionsMonitor<T>) don't change.
                .ConfigureRefresh(refresh => refresh.RegisterAll().SetRefreshInterval(TimeSpan.FromMinutes(5)));
        });

        // Register the refresher services consumed by the UseAzureAppConfiguration middleware.
        // Only registered when App Config is wired (cloud); locally this method returns early above.
        builder.Services.AddAzureAppConfiguration();

        return builder;
    }

    // Configures JWT bearer authentication against Auth0 for every backend API, so each service
    // validates tokens identically. Call this from each API's Program.cs after AddServiceDefaults.
    public static TBuilder AddDefaultAuthentication<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var authority = builder.Configuration["Auth0:Domain"] ?? DefaultAuthority;
        var audience = builder.Configuration["Auth0:Audience"] ?? DefaultAudience;

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.MapInboundClaims = false;
            });

        builder.Services.AddAuthorization();

        return builder;
    }

    public static TBuilder AddDefaultCors<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? DefaultCorsOrigins;

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(DefaultCorsPolicyName, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod());
        });

        return builder;
    }

    public static WebApplication UseDefaultCors(this WebApplication app)
    {
        app.UseCors(DefaultCorsPolicyName);
        return app;
    }

    /// <summary>
    /// Adds the Azure App Configuration middleware, which triggers a (non-blocking) refresh of
    /// configuration and feature flags on incoming requests, honoring the configured refresh
    /// interval. Without this, App Configuration — including feature flags — is loaded once at
    /// startup and never re-read, so flag toggles never take effect until a container restart.
    /// No-op locally, where the refresher services are not registered (App Config is absent).
    /// </summary>
    public static WebApplication UseAppConfigurationRefresh(this WebApplication app)
    {
        if (app.Services.GetService<IConfigurationRefresherProvider>() is not null)
        {
            app.UseAzureAppConfiguration();
        }

        return app;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // Exclude health check requests from tracing. Set on the options rather than in the
        // AddAspNetCoreInstrumentation call, so it holds whoever registers that instrumentation.
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
            options.Filter = context =>
                !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                // OTEL_SERVICE_NAME wins when set; without it the role name is the application name
                // (e.g. HexMaster.ThePrey.Games.Api) instead of "unknown_service".
                if (string.IsNullOrWhiteSpace(builder.Configuration[ServiceNameVariable]))
                {
                    resource.AddService(builder.Environment.ApplicationName, autoGenerateServiceInstanceId: false);
                }

                // service.instance.id is the replica (cloud_RoleInstance in Application Insights).
                resource.AddAttributes(
                [
                    new("service.version", ServiceVersion()),
                    new("service.instance.id", Environment.MachineName),
                ]);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(ProjectTelemetryPrefix);
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource(ProjectTelemetryPrefix)
                    .AddAspNetCoreInstrumentation()
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    // The host's informational version (the release semVer in CI builds).
    private static string ServiceVersion() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // In Azure (Container Apps) traces, metrics and logs flow to Application Insights through the
        // Azure Monitor distro; the container app injects APPLICATIONINSIGHTS_CONNECTION_STRING. Locally
        // it is absent and only the OTLP exporter (Aspire dashboard) runs. Every host is a WebApplication.
        var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
            {
                options.ConnectionString = appInsightsConnectionString;
                // Keep every trace: traffic is low and a complete game trace matters more than volume.
                // The distro's default is rate-limited sampling; lower SamplingRatio here if ingestion grows.
                options.TracesPerSecond = null;
                options.SamplingRatio = 1.0F;
            });
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
