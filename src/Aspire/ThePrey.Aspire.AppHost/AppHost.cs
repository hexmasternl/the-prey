using ThePrey.Aspire.ServiceDefaults;

var builder = DistributedApplication.CreateBuilder(args);


builder.AddProject<Projects.ThePrey_Application_App>(AspireConstants.Resources.MauiApp);

builder.AddProject<Projects.HexMaster_ThePrey_Users_Api>(AspireConstants.Resources.UsersApi);

builder.AddProject<Projects.HexMaster_ThePrey_PlayFields_Api>("hexmaster-theprey-playfields-api");

builder.Build().Run();
