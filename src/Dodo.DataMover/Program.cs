using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Formatting.Elasticsearch;

namespace Dodo.DataMover
{
    internal class Program
    {
        private static readonly string Version = typeof(Program).Assembly.GetName().Version!.ToString();

        private static async Task<int> Main(string[] args)
        {
            var helpSwitches = new[] {"-h", "--help", "/?"};
            if (args.Any(helpSwitches.Contains))
            {
                await DisplayUsage();
                Environment.Exit(0);
            }

            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var configuration = Configure(environmentName, args);
            try
            {
                Log.Debug("Configuring...");
                var services = new ServiceCollection();
                var startup = new Startup(configuration);
                startup.ConfigureServices(services);
                var serviceProvider = services.BuildServiceProvider();
                var app = serviceProvider.GetService<App>();
                Log.Debug("Running...");
                await app.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{@EventType}", "Main_Failed");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static async Task DisplayUsage()
        {
            var text = @"USAGE: ./Dodo.DataMover [options]
            {-h, --help, /?} - displays this text
            ConfigurationFilePath=<path> - optional json file path.
            For the complete schema see https://github.com/dodopizza/mysql-data-mover
";
            await Console.Out.WriteLineAsync(text);
        }

        private static IConfigurationRoot Configure(string environmentName, string[] args)
        {
            var builder = new ConfigurationBuilder();

            builder.AddJsonFile("appsettings.json", false, true);
            builder.AddJsonFile($"appsettings.{environmentName}.json", true, true);

            AddOptionalConfigurationFileOverride(args, builder);

            builder.AddEnvironmentVariables();
            builder.AddCommandLine(args);

            var configuration = builder.Build();

            ConfigureLogging(configuration);

            return configuration;
        }

        private static void AddOptionalConfigurationFileOverride(string[] args, ConfigurationBuilder builder)
        {
            var configurationFilePath = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build().GetSection("DataMover").GetValue<string>("ConfigurationFilePath");
            if (configurationFilePath != null)
            {
                var resolvedPath = Path.IsPathRooted(configurationFilePath)
                    ? configurationFilePath
                    : Path.Combine(Directory.GetCurrentDirectory(), configurationFilePath);
                builder.AddJsonFile(resolvedPath, false);
            }
        }

        private static void ConfigureLogging(IConfigurationRoot configuration)
        {
            Log.CloseAndFlush();
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.WithProperty("assemblyVersion", Version)
                .WriteTo.Async(config =>
                {
                    var formatter = new ExceptionAsObjectJsonFormatter(renderMessage: true, inlineFields: true);
                    config.Console(formatter);
                })
                .CreateLogger();
        }
    }
}
