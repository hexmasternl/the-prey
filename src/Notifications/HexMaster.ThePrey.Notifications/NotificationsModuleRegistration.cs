using HexMaster.ThePrey.Notifications.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.Notifications;

public static class NotificationsModuleRegistration
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddSingleton<INotificationsMetrics, NotificationsMetrics>();
        services.AddSingleton<IWebPubSubBroadcaster, WebPubSubBroadcaster>();
        return services;
    }
}
