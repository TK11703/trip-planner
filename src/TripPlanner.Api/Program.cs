using TripPlanner.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddTripPlannerApi();

var app = builder.Build();
app.UseTripPlannerApi();

// Apply schema scripts locally (Development) and in hosted environments when
// RunDatabaseMigrations is enabled (the production Postgres is internal-only, so
// migrations run from the API on startup rather than from the CI runner).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("RunDatabaseMigrations"))
{
    await app.InitializeDatabaseAsync();
}

app.Run();

public partial class Program;
