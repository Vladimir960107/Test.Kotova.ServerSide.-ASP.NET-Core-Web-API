using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// Configures the Lynks database context
        /// </summary>
        public static IServiceCollection ConfigureLynksDbContext(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<LynksDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("LynksDBConnection"),
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    }
                ));

            return services;
        }

        /// <summary>
        /// Configures the TransElectro (TELP) database context
        /// </summary>
        public static IServiceCollection ConfigureTransElectroDbContext(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<TransElectroDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("TransElectroDBConnection"),
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);

                        // Set command timeout for potentially large data operations
                        sqlOptions.CommandTimeout(120);
                    }
                ));

            return services;
        }

        /// <summary>
        /// Configures the Lynks database service
        /// </summary>
        public static IServiceCollection ConfigureLynksDbService(
            this IServiceCollection services)
        {
            services.AddScoped<ILynksDbService, LynksDbService>();
            return services;
        }

        /// <summary>
        /// Configures all database-related services
        /// </summary>
        public static IServiceCollection ConfigureAllDatabaseServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure both database contexts
            services.ConfigureLynksDbContext(configuration);
            services.ConfigureTransElectroDbContext(configuration);

            // Configure services
            services.ConfigureLynksDbService();

            return services;
        }

        /// <summary>
        /// Ensures both databases are available (without creating/migrating since TELP DB already exists)
        /// </summary>
        public static async Task<IServiceProvider> EnsureDatabasesAvailableAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            try
            {
                // Test and setup Lynks database (you have full rights here)
                var lynksContext = scope.ServiceProvider.GetRequiredService<LynksDbContext>();
                await lynksContext.Database.EnsureCreatedAsync();

                // Apply any pending migrations for Lynks
                if ((await lynksContext.Database.GetPendingMigrationsAsync()).Any())
                {
                    await lynksContext.Database.MigrateAsync();
                }

                // ONLY test TransElectro database connection (read-only)
                var telpContext = scope.ServiceProvider.GetRequiredService<TransElectroDbContext>();
                await telpContext.Database.CanConnectAsync();

                var logger = scope.ServiceProvider.GetService<ILogger<IServiceProvider>>();
                logger?.LogInformation("Database connections verified successfully");
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetService<ILogger<IServiceProvider>>();
                logger?.LogError(ex, "Error verifying database connections");
                throw;
            }

            return serviceProvider;
        }

        /// <summary>
        /// Creates sync log table in TELP database if it doesn't exist
        /// </summary>
        
    }
}