using TripPlanner.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddTripPlannerWeb();

var app = builder.Build();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseTripPlannerWeb();
app.Run();

public partial class Program;
