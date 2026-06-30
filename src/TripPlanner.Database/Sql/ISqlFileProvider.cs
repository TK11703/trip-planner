namespace TripPlanner.Database.Sql;

public interface ISqlFileProvider
{
    string Get(string relativePath);
    IReadOnlyList<(string Name, string Sql)> GetAllInDirectory(string relativeDirectory);
}
