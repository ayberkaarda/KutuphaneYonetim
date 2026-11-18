using Microsoft.Extensions.Configuration;

namespace KutuphaneYonetim.UI.Tests;

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

        var connectionString = configuration.GetConnectionString("Kutuphane");

        Assert.NotNull(connectionString);
        Assert.Contains(@"Server=localhost\SQLEXPRESS;", connectionString, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\\", connectionString, StringComparison.Ordinal);
    }
}
