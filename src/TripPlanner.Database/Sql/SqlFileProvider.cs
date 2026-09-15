using System.Collections.Concurrent;
using System.Globalization;

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

    /// <summary>
    /// Returns every script in <paramref name="relativeDirectory"/> in a stable total order.
    /// </summary>
    /// <remarks>
    /// Ordering is by the leading numeric prefix, then by ordinal file name. Several scripts
    /// share a prefix (for example <c>003_theme_preferences.sql</c> and
    /// <c>003_user_profiles.sql</c>), so the name is the tie-breaker. Ordinal comparison is
    /// used deliberately: a culture-sensitive sort would let the same commit produce a
    /// different migration order on a different machine.
    /// </remarks>
    public IReadOnlyList<(string Name, string Sql)> GetAllInDirectory(string relativeDirectory)
    {
        var directory = ResolveFullPath(relativeDirectory);
        if (!Directory.Exists(directory)) return Array.Empty<(string, string)>();
        return Directory.GetFiles(directory, "*.sql")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(GetOrderingPrefix)
            .ThenBy(name => name, StringComparer.Ordinal)
            .Select(name => (name, File.ReadAllText(Path.Combine(directory, name))))
            .ToArray();
    }

    /// <summary>
    /// Extracts the leading numeric prefix of a script name, or <see cref="int.MaxValue"/>
    /// when the name is not numbered so unnumbered scripts sort last rather than first.
    /// </summary>
    private static int GetOrderingPrefix(string fileName)
    {
        var separator = fileName.IndexOf('_');
        if (separator <= 0) return int.MaxValue;

        return int.TryParse(
            fileName.AsSpan(0, separator),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var prefix)
            ? prefix
            : int.MaxValue;
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
