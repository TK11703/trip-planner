var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgres-user", secret: false);
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword)
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var tripPlannerDb = postgres.AddDatabase("tripplanner");

var api = builder.AddProject<Projects.TripPlanner_Api>("api")
    .WithReference(tripPlannerDb)
    .WaitFor(tripPlannerDb);

builder.AddProject<Projects.TripPlanner_Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
