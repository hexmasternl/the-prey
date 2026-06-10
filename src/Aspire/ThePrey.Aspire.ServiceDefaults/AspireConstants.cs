using System;
using System.Collections.Generic;
using System.Text;

namespace ThePrey.Aspire.ServiceDefaults
{
    public static class AspireConstants
    {

        public static class Resources
        {
            public const string UsersApi = "hexmaster-theprey-users-api";
            public const string PlayFieldsApi = "hexmaster-theprey-playfields-api";
            public const string Storage = "storage";
            public const string PlayFieldsTables = "playfields-tables";
            public const string GamesApi = "hexmaster-theprey-games-api";
            public const string Postgres = "postgres";

            /// <summary>The PostgreSQL database (and Aspire connection) name; must match the Games data adapter's connection name.</summary>
            public const string GamesDatabase = "games";
            public const string Gateway = "gateway";
            public const string UsersTables = "users-tables";
            public const string DaprStateStore = "statestore";
            public const string Redis = "redis";

            /// <summary>
            /// The Dapr pub/sub component name. MUST match
            /// <c>DaprIntegrationEventPublisher.PubSubName</c> and the cloud Dapr component.
            /// Backed by RabbitMQ locally and Azure Service Bus in the cloud.
            /// </summary>
            public const string DaprPubSub = "pubsub";

            /// <summary>The RabbitMQ broker that backs the Dapr pub/sub component for local development.</summary>
            public const string RabbitMq = "rabbitmq";

            /// <summary>The Notifications API — bridges integration events to Azure Web PubSub.</summary>
            public const string NotificationsApi = "hexmaster-theprey-notifications-api";

            /// <summary>The Azure Web PubSub resource used to fan out real-time events to clients.</summary>
            public const string WebPubSub = "webpubsub";

            /// <summary>The Web PubSub hub name clients connect to.</summary>
            public const string WebPubSubHub = "games";
        }

    }
}
