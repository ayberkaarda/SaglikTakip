using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace SaglikTakip.Data.Tests;

/// <summary>
/// Throw-away database on a real SQL Server instance. Set SAGLIKTAKIP_TEST_SQLSERVER to a
/// server-level connection string to choose the instance (default: local SQL Express with Windows
/// authentication) and SAGLIKTAKIP_SKIP_SQL_TESTS=1 to skip every test that needs SQL Server.
/// </summary>
internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const string ServerEnvironmentVariable = "SAGLIKTAKIP_TEST_SQLSERVER";
    private const string SkipEnvironmentVariable = "SAGLIKTAKIP_SKIP_SQL_TESTS";
    private const string DefaultServer =
        @"Server=localhost\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;";

    private SqlServerTestDatabase(string server, string name)
    {
        Server = server;
        Name = name;
        ConnectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = name }.ConnectionString;
    }

    public string Server { get; }

    public string Name { get; }

    public string ConnectionString { get; }

    /// <summary>Creates an empty database, or skips the test when no SQL Server is available.</summary>
    public static async Task<SqlServerTestDatabase> CreateAsync(CancellationToken ct)
    {
        var database = await ReserveAsync(ct);
        await ExecuteAsync(database.Server, "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name); EXEC(@sql);", ct, ("@name", database.Name));
        return database;
    }

    /// <summary>Picks a server and a unique database name without creating the database.</summary>
    public static async Task<SqlServerTestDatabase> ReserveAsync(CancellationToken ct)
    {
        var server = await ResolveServerOrSkipAsync(ct);
        return new SqlServerTestDatabase(server, "SaglikTakipTest_" + Guid.NewGuid().ToString("N"));
    }

    public ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSqlServerDataAccess(ConnectionString);
        Business.BusinessServiceCollectionExtensions.AddBusinessServices(services);
        return services.BuildServiceProvider();
    }

    public Task ExecuteAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters) =>
        ExecuteAsync(ConnectionString, sql, ct, parameters);

    public Task<int> ScalarAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters) =>
        ScalarAsync(ConnectionString, sql, ct, parameters);

    public static async Task ExecuteAsync(
        string connectionString,
        string sql,
        CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(ct);
    }

    public static async Task<int> ScalarAsync(
        string connectionString,
        string sql,
        CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await ExecuteAsync(
            Server,
            """
            IF DB_ID(@name) IS NOT NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name) + N';';
                EXEC(@sql);
            END
            """,
            CancellationToken.None,
            ("@name", Name));
    }

    private static async Task<string> ResolveServerOrSkipAsync(CancellationToken ct)
    {
        var skip = Environment.GetEnvironmentVariable(SkipEnvironmentVariable);
        if (skip is "1" || string.Equals(skip, "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip($"{SkipEnvironmentVariable} is set.");
        }

        var server = Environment.GetEnvironmentVariable(ServerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            server = DefaultServer;
        }

        try
        {
            await using var connection = new SqlConnection(server);
            await connection.OpenAsync(ct);
        }
        catch (SqlException ex)
        {
            Assert.Skip($"No SQL Server available ({ex.Message}). Set {ServerEnvironmentVariable} to run this test.");
        }

        return server;
    }
}
