using System.Data.Common;
namespace TripPlanner.Database.Connections;

public interface IPostgresConnectionFactory
{
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
