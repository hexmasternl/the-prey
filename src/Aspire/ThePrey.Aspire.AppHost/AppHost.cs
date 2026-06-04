using ThePrey.Aspire.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

var usersApi = builder.AddProject<Projects.HexMaster_ThePrey_Users_Api>(AspireConstants.Resources.UsersApi)
    .WithDaprSidecar();

var storage = builder.AddAzureStorage(AspireConstants.Resources.Storage)
    .RunAsEmulator();

var usersTables = storage.AddTables(AspireConstants.Resources.UsersTables);

usersApi
    .WithReference(usersTables)
    .WaitFor(usersTables);

var playFieldsTables = storage.AddTables(AspireConstants.Resources.PlayFieldsTables);

var playFieldsApi = builder.AddProject<Projects.HexMaster_ThePrey_PlayFields_Api>(AspireConstants.Resources.PlayFieldsApi)
    .WithReference(playFieldsTables)
    .WaitFor(playFieldsTables);

var postgres = builder.AddPostgres(AspireConstants.Resources.Postgres);

var gamesDatabase = postgres.AddDatabase(AspireConstants.Resources.GamesDatabase);

var gamesApi = builder.AddProject<Projects.HexMaster_ThePrey_Games_Api>(AspireConstants.Resources.GamesApi)
    .WithReference(gamesDatabase)
    .WaitFor(gamesDatabase);

var gateway = builder.AddYarp(AspireConstants.Resources.Gateway)
    .WithHttpEndpoint(port: 5000)
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/users/{**catch-all}", usersApi);
        yarp.AddRoute("/playfields/{**catch-all}", playFieldsApi);
        yarp.AddRoute("/games/{**catch-all}", gamesApi);
    });


builder.Build().Run();
