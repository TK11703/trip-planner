using System.Data.Common;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TripPlanner.Database.Connections;

public sealed class PostgresConnectionFactory : IPostgresConnectionFactory, IAsyncDisposable
{
    // Azure Database for PostgreSQL accepts an Entra access token for this scope in place
    // of a password. Tokens last an hour, so refresh well inside that window.
    private static readonly string[] PostgresScope = ["https://ossrdbms-aad.database.windows.net/.default"];
    private static readonly TimeSpan TokenRefreshInterval = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan TokenRetryInterval = TimeSpan.FromSeconds(10);

    private readonly NpgsqlDataSource _dataSource;

    public PostgresConnectionFactory(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("tripplanner")
            ?? configuration["ConnectionStrings:tripplanner"]
            ?? configuration["Postgres:ConnectionString"]
            ?? throw new InvalidOperationException("PostgreSQL connection string 'tripplanner' is not configured.");

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        // A connection string with no password is the passwordless production path: the
        // managed identity's access token is the credential. Local and test runs supply a
        // password and never reach for a token.
        if (string.IsNullOrEmpty(builder.ConnectionStringBuilder.Password))
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = configuration["AZURE_CLIENT_ID"]
            });

            builder.UsePeriodicPasswordProvider(
                async (_, cancellationToken) =>
                {
                    var token = await credential.GetTokenAsync(new TokenRequestContext(PostgresScope), cancellationToken);
                    return token.Token;
                },
                TokenRefreshInterval,
                TokenRetryInterval);
        }

        _dataSource = builder.Build();
    }

    public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        => await _dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
