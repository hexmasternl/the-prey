using System;
using System.Collections.Generic;
using System.Text;

namespace ThePrey.Aspire.ServiceDefaults
{
    public static class AspireConstants
    {

        public static class Resources
        {
            public const string MauiApp = "theprey-application-app";
            public const string UsersApi = "hexmaster-theprey-users-api";
            public const string PlayFieldsApi = "hexmaster-theprey-playfields-api";
            public const string Storage = "storage";
            public const string PlayFieldsTables = "playfields-tables";
            public const string GamesApi = "hexmaster-theprey-games-api";
            public const string Postgres = "postgres";

            /// <summary>The PostgreSQL database (and Aspire connection) name; must match the Games data adapter's connection name.</summary>
            public const string GamesDatabase = "games";
            public const string Gateway = "gateway";
        }

    }
}
