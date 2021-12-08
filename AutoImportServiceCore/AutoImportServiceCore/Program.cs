using AutoImportServiceCore.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AutoImportServiceCore.Core.Workers;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Modules.Payments.Interfaces;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AutoImportServiceCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    ConfigureSettings(hostContext.Configuration, services);
                    ConfigureHostedServices(services);
                    ConfigureAisServices(services);
                    services.AddLogging(builder => { builder.AddSerilog(); });
                });

        private static void ConfigureSettings(IConfiguration configuration, IServiceCollection services)
        {
            services.Configure<AisSettings>(configuration.GetSection("Ais"));

            services.AddScoped<ConfigurationsWorker>();
        }

        private static void ConfigureHostedServices(IServiceCollection services)
        {
            services.AddHostedService<MainWorker>();
        }

        private static void ConfigureAisServices(IServiceCollection services)
        {
            // Configure automatic scanning of classes for dependency injection.
            services.Scan(scan => scan
                // We start out with all types in the current assembly.
                .FromCallingAssembly()
                // AddClasses starts out with all public, non-abstract types in this assembly.
                // These types are then filtered by the delegate passed to the method.
                // In this case, we filter out only the classes that are assignable to ITransientService.
                .AddClasses(classes => classes.AssignableTo<ITransientService>())
                // We then specify what type we want to register these classes as.
                // In this case, we want to register the types as all of its implemented interfaces.
                // So if a type implements 3 interfaces; A, B, C, we'd end up with three separate registrations.
                .AsImplementedInterfaces()
                // And lastly, we specify the lifetime of these registrations.
                .WithTransientLifetime()
                // Here we start again, with a new full set of classes from the assembly above.
                // This time, filtering out only the classes assignable to IScopedService.
                .AddClasses(classes => classes.AssignableTo<IScopedService>())
                .AsImplementedInterfaces()
                .WithScopedLifetime()
                // Here we start again, with a new full set of classes from the assembly above.
                // This time, filtering out only the classes assignable to IScopedService.
                .AddClasses(classes => classes.AssignableTo<ISingletonService>())
                .AsImplementedInterfaces()
                .WithSingletonLifetime()

                // Payment service providers need to be added with their own type, otherwise the factory won't work.
                .AddClasses(classes => classes.AssignableTo<IPaymentServiceProviderService>())
                .AsSelf()
                .WithScopedLifetime());
        }
    }
}