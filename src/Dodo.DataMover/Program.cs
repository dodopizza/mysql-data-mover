using System;
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

        private static IConfigurationRoot Configure(string environmentName, string[] args)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
                .AddJsonFile($"appsettings.{environmentName}.json", true, true)
                .AddEnvironmentVariables()
                .AddCommandLine(args);

            var configuration = builder.Build();

            ConfigureLogging(configuration, environmentName);

            return configuration;
        }

        private static void ConfigureLogging(IConfigurationRoot configuration, string environmentName)
        {
            var isDevelopment = string.Equals(
                environmentName,
                "local",
                StringComparison.OrdinalIgnoreCase);
            var useJsonStdout = !isDevelopment;

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.WithProperty("assemblyVersion", Version)
                .WriteTo.Async(config =>
                {
                    if (useJsonStdout)
                    {
                        var formatter = new ExceptionAsObjectJsonFormatter(renderMessage: true, inlineFields: true);
                        config.Console(formatter);
                    }
                    else
                    {
                        config.ColoredConsole();
                    }
                })
                .CreateLogger();
        }
    }
}
