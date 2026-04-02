using FluentAssertions;
using SmartStockAI.Data.Context;

namespace SmartStockAI.Tests;

public class SqliteConnectionStringResolverTests
{
    [Fact]
    public void Resolve_ShouldConvertRelativeDatabasePathToSolutionRootAbsolutePath()
    {
        var connectionString = SqliteConnectionStringResolver.Resolve("Data Source=smartstockai.db");
        var resolvedPath = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;

        connectionString.Should().Contain("Data Source=");
        Path.GetFileName(resolvedPath).Should().Be("smartstockai.db");
        Path.GetDirectoryName(resolvedPath).Should().Be(SqliteConnectionStringResolver.GetDatabaseBasePath());
        Path.IsPathRooted(resolvedPath)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void GetDatabaseBasePath_ShouldPointToSolutionRoot()
    {
        var basePath = SqliteConnectionStringResolver.GetDatabaseBasePath();

        File.Exists(Path.Combine(basePath, "SmartStockAI.sln")).Should().BeTrue();
    }
}
