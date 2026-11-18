using Microsoft.Extensions.Configuration;
using SaglikTakip.Data;

namespace SaglikTakip.UI.Tests;

public sealed class ConfigurationTests
{
    private const string ExampleFileName = "appsettings.example.json";

    [Fact]
    public void ExampleConfiguration_LoadsWithNamedInstance()
    {
        Assert.True(
            File.Exists(Path.Combine(AppContext.BaseDirectory, ExampleFileName)),
            $"{ExampleFileName} was not copied next to the tests.");

        // Same loader the application uses for appsettings.json; a malformed file throws here.
        var configuration = new ConfigurationBuilder()
            .AddSettingsFile(AppContext.BaseDirectory, ExampleFileName)
            .Build();

        var connectionString = AppConfiguration.ResolveConnectionString(configuration);

        Assert.NotNull(connectionString);
        Assert.Contains(@"Server=localhost\SQLEXPRESS;", connectionString, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\\", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentVariable_OverridesSettingsFile()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SaglikTakip"] = "Server=from-file;",
                [DataServiceCollectionExtensions.ConnectionEnvironmentVariable] = "Server=from-environment;",
            })
            .Build();

        Assert.Equal("Server=from-environment;", AppConfiguration.ResolveConnectionString(configuration));
    }
}
