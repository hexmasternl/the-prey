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
            public const string GameEngineQueue = "game-engine-queue";
            public const string GameEngine = "hexmaster-theprey-game-engine";
        }

        public static class Queues
        {
            /// <summary>
            /// Name of the storage queue the Games API writes to when a game starts and the
            /// game engine job reads from / is KEDA-triggered by. Must match the queue provisioned
            /// in the landing zone and the job's azure-queue scale rule.
            /// </summary>
            public const string GameStart = "gamestart";
        }

    }
}
