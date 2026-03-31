using Microsoft.Data.Sqlite;

namespace SmartStockAI.Data.Context;

public static class SqliteConnectionStringResolver
{
    private const string DefaultDatabaseFileName = "smartstockai.db";
    private const string SolutionFileName = "SmartStockAI.sln";

    public static string Resolve(string? connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(
            string.IsNullOrWhiteSpace(connectionString)
                ? $"Data Source={DefaultDatabaseFileName}"
                : connectionString);

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            builder.DataSource = DefaultDatabaseFileName;
        }

        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(GetDatabaseBasePath(), builder.DataSource);
        }

        return builder.ToString();
    }

    public static string GetDatabaseBasePath()
    {
        foreach (var startPath in EnumerateCandidateRoots())
        {
            var solutionRoot = TryFindSolutionRoot(startPath);
            if (solutionRoot is not null)
            {
                return solutionRoot;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();

        var entryAssemblyLocation = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(entryAssemblyLocation))
        {
            var entryAssemblyDirectory = Path.GetDirectoryName(entryAssemblyLocation);
            if (!string.IsNullOrWhiteSpace(entryAssemblyDirectory))
            {
                yield return entryAssemblyDirectory;
            }
        }
    }

    private static string? TryFindSolutionRoot(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath))
        {
            return null;
        }

        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, SolutionFileName);
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
