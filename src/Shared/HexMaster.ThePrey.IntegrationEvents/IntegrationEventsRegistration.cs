using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.IntegrationEvents;

public static class IntegrationEventsRegistration
{
    /// <summary>
    /// Registers the Dapr-backed integration event publisher. The host is expected to register a
    /// <c>DaprClient</c> (e.g. via <c>AddServiceDefaults()</c>, which calls <c>AddDaprClient()</c>).
    /// </summary>
    public static IServiceCollection AddIntegrationEvents(this IServiceCollection services)
    {
        services.AddSingleton<IIntegrationEventPublisher, DaprIntegrationEventPublisher>();
        return services;
    }
}
