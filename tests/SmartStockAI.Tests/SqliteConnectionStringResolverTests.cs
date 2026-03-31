using FluentAssertions;
using SmartStockAI.Data.Context;

namespace SmartStockAI.Tests;

public class SqliteConnectionStringResolverTests
{
    [Fact]
    public void Resolve_ShouldConvertRelativeDatabasePathToSolutionRootAbsolutePath()
    {
        var connectionString = SqliteConnectionStringResolver.Resolve("Data Source=smartstockai.db");

        connectionString.Should().Contain("Data Source=");
        connectionString.Should().Contain(Path.Combine("warehouseAIaccounting", "smartstockai.db"));
        Path.IsPathRooted(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource)
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
