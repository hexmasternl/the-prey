var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ThePrey_Users_Api>("theprey-users-api");

builder.AddProject<Projects.ThePrey_Application_App>("theprey-application-app");

builder.Build().Run();
