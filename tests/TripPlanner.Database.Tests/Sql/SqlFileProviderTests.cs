using System.Globalization;
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

    [Fact]
    public void Provider_OrdersSchemaScriptsByNumericPrefixNotLexically()
    {
        var sql = new SqlFileProvider();
        var names = sql.GetAllInDirectory("Schema").Select(s => s.Name).ToArray();

        var prefixes = names
            .Select(name => int.Parse(name[..name.IndexOf('_')], CultureInfo.InvariantCulture))
            .ToArray();

        Assert.Equal(prefixes.OrderBy(p => p), prefixes);
    }

    [Fact]
    public void Provider_BreaksPrefixTiesDeterministically()
    {
        var sql = new SqlFileProvider();

        var first = sql.GetAllInDirectory("Schema").Select(s => s.Name).ToArray();
        var second = new SqlFileProvider().GetAllInDirectory("Schema").Select(s => s.Name).ToArray();

        Assert.Equal(first, second);

        // 003_ and 009_ are shared by two scripts each; the file name is the tie-breaker.
        var shared = first.Where(name => name.StartsWith("003_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(shared.OrderBy(n => n, StringComparer.Ordinal), shared);
    }
}
