using Dodo.DataMover.DataManipulation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace Dodo.DataMover
{
    public class Startup
    {
        private readonly IConfigurationRoot _configuration;

        public Startup(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureLogging(services);
            ConfigureDependencyInjection(services);
        }

        private static void ConfigureLogging(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddSerilog());
        }

        private void ConfigureDependencyInjection(IServiceCollection services)
        {
            RegisterSettings(services);
            RegisterServices(services);
        }

        private void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<SourceSchemaReader>();
            services.AddSingleton<DatabasePublisher>();
            services.AddSingleton<SourceDataReader>();
            services.AddSingleton<ReadCommandGenerator>();
            services.AddSingleton<SchemaCopier>();
            services.AddSingleton<PolicyFactory>();
            services.AddTransient<App>();
        }

        private void RegisterSettings(IServiceCollection services)
        {
            services.AddSettingsSingleton<DataMoverSettings>(_configuration, "DataMover");
        }
    }

    public static class OptionsExtensions
    {
        public static IServiceCollection AddSettingsSingleton<T>(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName)
            where T : class
        {
            return services
                .Configure<T>(configuration
                    .GetSection(sectionName))
                .AddSingleton(sp => sp
                    .GetRequiredService<IOptionsMonitor<T>>().CurrentValue);
        }
    }
}
