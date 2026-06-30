using System.Collections.Concurrent;

namespace TripPlanner.Database.Sql;

public sealed class SqlFileProvider : ISqlFileProvider
{
    private readonly string _rootDirectory;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SqlFileProvider() : this(LocateScriptsRoot()) { }

    public SqlFileProvider(string rootDirectory)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
    }

    public string RootDirectory => _rootDirectory;

    public string Get(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("Relative path required.", nameof(relativePath));
        return _cache.GetOrAdd(relativePath, path =>
        {
            var fullPath = ResolveFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"SQL file not found: {path}", fullPath);
            return File.ReadAllText(fullPath);
        });
    }

    public IReadOnlyList<(string Name, string Sql)> GetAllInDirectory(string relativeDirectory)
    {
        var directory = ResolveFullPath(relativeDirectory);
        if (!Directory.Exists(directory)) return Array.Empty<(string, string)>();
        return Directory.GetFiles(directory, "*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)))
            .ToArray();
    }

    private string ResolveFullPath(string relative)
    {
        var normalized = relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return Path.Combine(_rootDirectory, normalized);
    }

    private static string LocateScriptsRoot()
    {
        var asmDir = Path.GetDirectoryName(typeof(SqlFileProvider).Assembly.Location) ?? AppContext.BaseDirectory;
        var candidate = Path.Combine(asmDir, "Scripts");
        if (Directory.Exists(candidate)) return candidate;

        var dir = new DirectoryInfo(asmDir);
        while (dir is not null)
        {
            var scripts = Path.Combine(dir.FullName, "src", "TripPlanner.Database", "Scripts");
            if (Directory.Exists(scripts)) return scripts;
            scripts = Path.Combine(dir.FullName, "TripPlanner.Database", "Scripts");
            if (Directory.Exists(scripts)) return scripts;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "Scripts");
    }
}
