using TripPlanner.Database.Sql;
using Xunit;

namespace TripPlanner.Database.Tests.Sql;

public class SqlFileProviderTests
{
    [Fact]
    public void Provider_LoadsRecentTripsSql()
    {
        var sql = new SqlFileProvider();
        var contents = sql.Get("Queries/Trips/GetRecentTrips.sql");
        Assert.Contains("FROM trips", contents);
        Assert.Contains("@OwnerUserId", contents);
    }

    [Fact]
    public void Provider_EnumeratesSchemaScripts()
    {
        var sql = new SqlFileProvider();
        var scripts = sql.GetAllInDirectory("Schema");
        Assert.NotEmpty(scripts);
        Assert.Equal("000_init.sql", scripts[0].Name);
    }
}
