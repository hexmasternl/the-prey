using ThePrey.Aspire.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);


builder.AddProject<Projects.ThePrey_Application_App>(AspireConstants.Resources.MauiApp);

builder.AddProject<Projects.HexMaster_ThePrey_Users_Api>(AspireConstants.Resources.UsersApi);

var storage = builder.AddAzureStorage(AspireConstants.Resources.Storage)
    .RunAsEmulator();

var playFieldsTables = storage.AddTables(AspireConstants.Resources.PlayFieldsTables);

builder.AddProject<Projects.HexMaster_ThePrey_PlayFields_Api>(AspireConstants.Resources.PlayFieldsApi)
    .WithReference(playFieldsTables)
    .WaitFor(playFieldsTables);

var postgres = builder.AddPostgres(AspireConstants.Resources.Postgres);

var gamesDatabase = postgres.AddDatabase(AspireConstants.Resources.GamesDatabase);

builder.AddProject<Projects.HexMaster_ThePrey_Games_Api>(AspireConstants.Resources.GamesApi)
    .WithReference(gamesDatabase)
    .WaitFor(gamesDatabase);

builder.Build().Run();
