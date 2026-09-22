using Npgsql;
using Testcontainers.PostgreSql;

namespace Katalog.Api.Tests.Integration;

/// <summary>
/// A shared Postgres test container with a "catalog" database, started once per test class
/// and reset (tables dropped) between tests so each test starts from an empty schema.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string CatalogDatabase = "catalog";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18.3")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string CatalogConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Create the application database, then rewrite the connection string to point at it.
        await using var master = new NpgsqlConnection(_container.GetConnectionString());
        await master.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {CatalogDatabase}", master);
        await create.ExecuteNonQueryAsync();

        CatalogConnectionString = _container.GetConnectionString().Replace("Database=postgres", $"Database={CatalogDatabase}");
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
