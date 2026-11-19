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

    [Fact]
    public void ExampleConfiguration_ContainsValidLibraryPolicy()
    {
        var configuration = new ConfigurationBuilder()
            .AddSettingsFile(AppContext.BaseDirectory, ExampleFileName)
            .Build();

        var (policy, problems) = AppConfiguration.ReadLibraryPolicy(configuration);

        Assert.Empty(problems);
        Assert.Equal(14, policy.LoanPeriodDays);
        Assert.Equal(2.50m, policy.DailyFine);
        Assert.Equal(0m, policy.MaxUnpaidFine);
        Assert.Equal(3, policy.ReservationHoldDays);
    }

    [Fact]
    public void LibraryPolicy_MissingSection_UsesDefaults()
    {
        var (policy, problems) = AppConfiguration.ReadLibraryPolicy(new ConfigurationBuilder().Build());

        Assert.Empty(problems);
        Assert.Equal(new Business.LibraryPolicy().LoanPeriodDays, policy.LoanPeriodDays);
    }

    [Theory]
    [InlineData("LoanPeriodDays", "0")]
    [InlineData("LoanPeriodDays", "abc")]
    [InlineData("DailyFine", "-1")]
    [InlineData("ReservationHoldDays", "0")]
    public void LibraryPolicy_InvalidValues_AreReported(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Library:" + key] = value })
            .Build();

        var (_, problems) = AppConfiguration.ReadLibraryPolicy(configuration);

        Assert.NotEmpty(problems);
    }
}
