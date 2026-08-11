var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgres-user", secret: false);
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword)
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var tripPlannerDb = postgres.AddDatabase("tripplanner");

// Explicit launch profiles keep the HTTPS endpoints (api 7082, web 7203) primary
// regardless of which profile the AppHost itself was started with.
var api = builder.AddProject<Projects.TripPlanner_Api>("api", launchProfileName: "https")
    .WithReference(tripPlannerDb)
    .WaitFor(tripPlannerDb);

builder.AddProject<Projects.TripPlanner_Web>("web", launchProfileName: "https")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
