using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TripPlanner.Database.Connections;

public sealed class PostgresConnectionFactory : IPostgresConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("tripplanner")
            ?? configuration["ConnectionStrings:tripplanner"]
            ?? configuration["Postgres:ConnectionString"]
            ?? throw new InvalidOperationException("PostgreSQL connection string 'tripplanner' is not configured.");
        _connectionString = cs;
    }

    public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
