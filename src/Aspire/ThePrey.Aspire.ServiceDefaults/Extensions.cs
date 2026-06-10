using Azure.Monitor.OpenTelemetry.Exporter;
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
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

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
                .ConfigureKeyVault(keyVault => keyVault.SetCredential(credential));
        });

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

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // In Azure (Container Apps / jobs) telemetry flows to Application Insights via the
        // Azure Monitor exporter. The connection string is injected as APPLICATIONINSIGHTS_CONNECTION_STRING.
        // We attach the exporter to the existing OTel pipeline (traces, metrics, logs) rather than using
        // the AspNetCore distro, so we don't double-register instrumentation already configured above and
        // so the worker/job (a non-ASP.NET host) is covered too.
        var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddAzureMonitorTraceExporter(options =>
                    options.ConnectionString = appInsightsConnectionString))
                .WithMetrics(metrics => metrics.AddAzureMonitorMetricExporter(options =>
                    options.ConnectionString = appInsightsConnectionString));

            builder.Logging.AddOpenTelemetry(logging =>
                logging.AddAzureMonitorLogExporter(options =>
                    options.ConnectionString = appInsightsConnectionString));
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
