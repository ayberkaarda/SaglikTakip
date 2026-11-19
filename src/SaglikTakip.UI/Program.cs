using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaglikTakip.Business;
using SaglikTakip.Data;
using SaglikTakip.Data.Persistence;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;
using Serilog;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SaglikTakip.UI.Tests")]

namespace SaglikTakip.UI
{
    internal static class Program
    {
        internal const string ConnectionStringName = "SaglikTakip";

        /// <summary>Application entry point and composition root.</summary>
        [STAThread]
        private static void Main()
        {
            Log.Logger = AppLogging.CreateLogger(AppLogging.DefaultDirectory);
            try
            {
                Log.Information("Application started");
                Run();
            }
            finally
            {
                Log.Information("Application stopped");
                Log.CloseAndFlush();
            }
        }

        private static void Run()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ErrorReporter.Show(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.IsTerminating)
                {
                    Log.Fatal("Unhandled exception terminates the application");
                }

                ErrorReporter.Show(e.ExceptionObject as Exception);
                if (e.IsTerminating)
                {
                    Log.CloseAndFlush();
                }
            };

            ApplicationConfiguration.Initialize();

            var configuration = BuildConfiguration();
            ApplyTheme(AppConfiguration.ResolveTheme(configuration));

            var connectionString = AppConfiguration.ResolveConnectionString(configuration);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Log.Warning("No database connection string is configured");
                MessageBox.Show(
                    "Veritabanı bağlantı ayarı bulunamadı.\n\n"
                    + "appsettings.example.json dosyasını appsettings.json olarak kopyalayıp bağlantı bilgisini girin "
                    + "ya da SAGLIKTAKIP_CONNECTION ortam değişkenini tanımlayın.",
                    "Sağlık Takip",
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

        /// <summary>Chooses the palette; in dark mode the native parts of the controls are switched to dark too.</summary>
        private static void ApplyTheme(ThemeMode mode)
        {
            var highContrast = SystemInformation.HighContrast;
            Theme.Initialize(mode, highContrast, Theme.SystemPrefersDark());
            if (Theme.Current.IsDark && !highContrast)
            {
#pragma warning disable WFO5001 // Dark mode of the native controls is marked experimental in Windows Forms.
                Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001
            }
        }

        private static IConfiguration BuildConfiguration() =>
            new ConfigurationBuilder()
                .AddSettingsFile(AppContext.BaseDirectory, AppConfiguration.SettingsFileName)
                .AddUserSecrets(typeof(Program).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

        private static ServiceProvider ConfigureServices(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddSqlServerDataAccess(connectionString);
            services.AddBusinessServices();
            services.AddSingleton<IFormFactory, FormFactory>();
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        }
    }

    /// <summary>How the application reads its settings.</summary>
    internal static class AppConfiguration
    {
        public const string SettingsFileName = "appsettings.json";
        public const string ThemeKey = "Appearance:Theme";

        /// <summary>Adds an optional JSON settings file located in <paramref name="basePath"/>.</summary>
        public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string basePath, string fileName) =>
            builder
                .SetBasePath(basePath)
                .AddJsonFile(fileName, optional: true, reloadOnChange: false);

        /// <summary>
        /// Resolution order (last wins): appsettings.json, user secrets, ConnectionStrings__SaglikTakip
        /// environment variable, SAGLIKTAKIP_CONNECTION environment variable.
        /// </summary>
        public static string? ResolveConnectionString(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var fromEnvironment = configuration[DataServiceCollectionExtensions.ConnectionEnvironmentVariable];
            return string.IsNullOrWhiteSpace(fromEnvironment)
                ? configuration.GetConnectionString(Program.ConnectionStringName)
                : fromEnvironment;
        }

        /// <summary><c>Appearance:Theme</c>: Light (default), Dark or System; unknown values fall back to Light.</summary>
        public static ThemeMode ResolveTheme(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            return Enum.TryParse<ThemeMode>(configuration[ThemeKey], ignoreCase: true, out var mode) && Enum.IsDefined(mode)
                ? mode
                : ThemeMode.Light;
        }
    }
}
