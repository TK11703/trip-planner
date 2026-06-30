using TripPlanner.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddTripPlannerApi();

var app = builder.Build();
app.UseTripPlannerApi();

if (app.Environment.IsDevelopment())
{
    await app.InitializeDatabaseAsync();
}

app.Run();

public partial class Program;
