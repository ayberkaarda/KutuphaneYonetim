using KutuphaneYonetim.Business;
using KutuphaneYonetim.Data;
using KutuphaneYonetim.Data.Persistence;
using KutuphaneYonetim.UI.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KutuphaneYonetim.UI.Tests")]

namespace KutuphaneYonetim.UI
{
    internal static class Program
    {
        private const string ConnectionStringName = "Kutuphane";

        /// <summary>Application entry point and composition root.</summary>
        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ErrorReporter.Show(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ErrorReporter.Show(e.ExceptionObject as Exception);

            ApplicationConfiguration.Initialize();

            var connectionString = ResolveConnectionString(BuildConfiguration());
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show(
                    "Veritabanı bağlantı ayarı bulunamadı.\n\n"
                    + "appsettings.example.json dosyasını appsettings.json olarak kopyalayıp bağlantı bilgisini girin "
                    + "ya da KUTUPHANE_CONNECTION ortam değişkenini tanımlayın.",
                    "Kütüphane Yönetim",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            using var serviceProvider = ConfigureServices(connectionString);

            try
            {
                // No synchronization context exists yet, so blocking here cannot deadlock.
                serviceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync().GetAwaiter().GetResult();
            }
#pragma warning disable CA1031 // Startup boundary: report and exit instead of crashing.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Show(ex);
                return;
            }

            var formFactory = serviceProvider.GetRequiredService<IFormFactory>();
            Application.Run(formFactory.Create<Form1>());
        }

        private static IConfiguration BuildConfiguration() =>
            new ConfigurationBuilder()
                .AddSettingsFile(AppContext.BaseDirectory, AppConfiguration.SettingsFileName)
                .AddUserSecrets(typeof(Program).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

        /// <summary>
        /// Resolution order (last wins): appsettings.json, user secrets, ConnectionStrings__Kutuphane
        /// environment variable, KUTUPHANE_CONNECTION environment variable.
        /// </summary>
        private static string? ResolveConnectionString(IConfiguration configuration)
        {
            var fromEnvironment = configuration[DataServiceCollectionExtensions.ConnectionEnvironmentVariable];
            return string.IsNullOrWhiteSpace(fromEnvironment)
                ? configuration.GetConnectionString(ConnectionStringName)
                : fromEnvironment;
        }

        private static ServiceProvider ConfigureServices(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddSqlServerDataAccess(connectionString);
            services.AddBusinessServices();
            services.AddSingleton<IFormFactory, FormFactory>();
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        }
    }

    /// <summary>How the application reads its JSON settings files.</summary>
    internal static class AppConfiguration
    {
        public const string SettingsFileName = "appsettings.json";

        /// <summary>Adds an optional JSON settings file located in <paramref name="basePath"/>.</summary>
        public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string basePath, string fileName) =>
            builder
                .SetBasePath(basePath)
                .AddJsonFile(fileName, optional: true, reloadOnChange: false);
    }
}
