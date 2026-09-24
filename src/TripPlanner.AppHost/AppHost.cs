var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgres-user", secret: false);
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword)
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var tripPlannerDb = postgres.AddDatabase("tripplanner");

// Explicit launch profiles keep the HTTPS endpoints (api 7082, web 7203) primary
// regardless of which profile the AppHost itself was started with.
var api = builder.AddProject<Projects.TripPlanner_Api>("api", launchProfileName: "https")
    .WithReference(tripPlannerDb)
    .WaitFor(tripPlannerDb);

// Pin which developer credential the API authenticates Azure OpenAI with locally.
//
// DefaultAzureCredential tries VisualStudioCredential before AzureCliCredential, and Visual
// Studio is frequently signed in as a different account than `az login`. It then returns a
// perfectly valid token that the data plane rejects with a 401 — which reads as a broken
// endpoint rather than the wrong identity, and is correspondingly slow to diagnose.
//
// `az login` is already the documented prerequisite for recognition, so the chain is reduced to
// that one credential. Only the local run is affected: hosted deployments never run the AppHost
// and continue to use managed identity. A developer who authenticates another way can override
// this by exporting AZURE_TOKEN_CREDENTIALS themselves before starting.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS")))
{
    api.WithEnvironment("AZURE_TOKEN_CREDENTIALS", "AzureCliCredential");
}

builder.AddProject<Projects.TripPlanner_Web>("web", launchProfileName: "https")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
