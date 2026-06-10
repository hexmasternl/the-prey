using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.Notifications;

public static class NotificationsModuleRegistration
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddSingleton<IWebPubSubBroadcaster, WebPubSubBroadcaster>();
        return services;
    }
}
