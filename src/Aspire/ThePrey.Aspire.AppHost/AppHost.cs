using ThePrey.Aspire.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

var stateStore = builder
    .AddDaprStateStore(AspireConstants.Resources.DaprStateStore)
    .WithMetadata("keyPrefix", "none")
    .WaitFor(redis);


var storage = builder.AddAzureStorage(AspireConstants.Resources.Storage)
    .RunAsEmulator(azurite =>
    {
        azurite.WithLifetime(ContainerLifetime.Persistent);
    });

var usersTables = storage.AddTables(AspireConstants.Resources.UsersTables);
var playFieldsTables = storage.AddTables(AspireConstants.Resources.PlayFieldsTables);

var usersApi = builder.AddProject<Projects.HexMaster_ThePrey_Users_Api>(AspireConstants.Resources.UsersApi)
    .WithDaprSidecar(dpr => dpr.WithStateStore(stateStore))
    .WithEnvironment("DAPR_APP_API_TOKEN", "dev-dapr-token")
    .WithReference(usersTables)
    .WaitFor(usersTables);


var playFieldsApi = builder.AddProject<Projects.HexMaster_ThePrey_PlayFields_Api>(AspireConstants.Resources.PlayFieldsApi)
    .WithReference(playFieldsTables)
    .WithDaprSidecar(dpr => dpr.WithStateStore(stateStore))
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
