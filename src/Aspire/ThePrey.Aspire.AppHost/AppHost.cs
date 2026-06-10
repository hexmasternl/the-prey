using CommunityToolkit.Aspire.Hosting.Dapr;
using ThePrey.Aspire.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);

//var redis = builder.AddRedis(AspireConstants.Resources.Redis);

var stateStore = builder
    .AddDaprStateStore(AspireConstants.Resources.DaprStateStore)
    .WithMetadata("keyPrefix", "none");

// RabbitMQ backs the Dapr pub/sub component for local development (Azure Service Bus is used in the
// cloud via a Bicep-provisioned Dapr component). Fixed credentials + host port keep the static
// component YAML (components/pubsub.yaml) connection string valid.
var rabbitUser = builder.AddParameter("rabbitmq-username", "guest");
var rabbitPassword = builder.AddParameter("rabbitmq-password", "guest", secret: true);
var rabbitmq = builder
    .AddRabbitMQ(AspireConstants.Resources.RabbitMq, rabbitUser, rabbitPassword, port: 5672)
    .WithLifetime(ContainerLifetime.Persistent);

var pubSub = builder.AddDaprPubSub(
    AspireConstants.Resources.DaprPubSub,
    new DaprComponentOptions { LocalPath = "components/pubsub.yaml" });

// Azure Web PubSub fans real-time game events out to clients (one group per game). There is no local
// emulator, so for local development supply a connection string via user secrets / configuration under
// the "webpubsub" connection name; in the cloud it is provisioned.
var webPubSub = builder.AddAzureWebPubSub(AspireConstants.Resources.WebPubSub);

var storage = builder.AddAzureStorage(AspireConstants.Resources.Storage)
    .RunAsEmulator(azurite =>        azurite.WithLifetime(ContainerLifetime.Persistent));
var usersTables = storage.AddTables(AspireConstants.Resources.UsersTables);
var playFieldsTables = storage.AddTables(AspireConstants.Resources.PlayFieldsTables);

var postgres = builder.AddPostgres(AspireConstants.Resources.Postgres);
var gamesDatabase = postgres.AddDatabase(AspireConstants.Resources.GamesDatabase);

var usersApi = builder.AddProject<Projects.HexMaster_ThePrey_Users_Api>(AspireConstants.Resources.UsersApi)
    .WaitFor(usersTables)
    .WithReference(usersTables)
    .WithDaprSidecar(opts =>
    {
        opts.WithReference(stateStore);
    });

var playFieldsApi = builder.AddProject<Projects.HexMaster_ThePrey_PlayFields_Api>(AspireConstants.Resources.PlayFieldsApi)
    .WithReference(playFieldsTables)
    .WithDaprSidecar(opts =>
    {
        opts.WithReference(stateStore);
    })
    .WaitFor(playFieldsTables);

var gamesApi = builder.AddProject<Projects.HexMaster_ThePrey_Games_Api>(AspireConstants.Resources.GamesApi)
    .WithReference(gamesDatabase)
    .WithReference(rabbitmq)
    .WithDaprSidecar(opts =>
    {
        opts.WithReference(stateStore);
        opts.WithReference(pubSub);
    })
    .WaitFor(gamesDatabase)
    .WaitFor(rabbitmq);

var notificationsApi = builder.AddProject<Projects.HexMaster_ThePrey_Notifications_Api>(AspireConstants.Resources.NotificationsApi)
    .WithReference(webPubSub)
    .WithReference(rabbitmq)
    .WithReference(gamesApi)
    .WithDaprSidecar(opts =>
    {
        opts.WithReference(pubSub);
    })
    .WaitFor(rabbitmq);

var gateway = builder.AddYarp(AspireConstants.Resources.Gateway)
    .WithHttpEndpoint(port: 5000)
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/users/{**catch-all}", usersApi);
        yarp.AddRoute("/playfields/{**catch-all}", playFieldsApi);
        yarp.AddRoute("/games/{**catch-all}", gamesApi);
        yarp.AddRoute("/notifications/{**catch-all}", notificationsApi);
    });


builder.Build().Run();
