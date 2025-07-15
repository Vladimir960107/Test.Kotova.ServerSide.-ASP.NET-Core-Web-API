using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

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
            var connectionString = configuration.GetConnectionString("LynksDBConnection");

            // Configure shared options
            Action<SqlServerDbContextOptionsBuilder> sqlOptions = sqlBuilder =>
            {
                sqlBuilder.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            };

            // Register the regular DbContext (for controllers that still use it)
            services.AddDbContext<LynksDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions));

            // Register the DbContextFactory for controllers that need concurrent operations
            // Use a separate registration that doesn't depend on the scoped DbContextOptions
            services.AddSingleton<IDbContextFactory<LynksDbContext>>(provider =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<LynksDbContext>();
                optionsBuilder.UseSqlServer(connectionString, sqlOptions);
                return new PooledDbContextFactory<LynksDbContext>(optionsBuilder.Options);
            });

            return services;
        }

        /// <summary>
        /// Configures the TransElectro (TELP) database context
        /// </summary>
        public static IServiceCollection ConfigureTransElectroDbContext(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("TransElectroDBConnection");

            // Configure shared options
            Action<SqlServerDbContextOptionsBuilder> sqlOptions = sqlBuilder =>
            {
                sqlBuilder.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            };

            services.AddDbContext<TransElectroDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions));

            // Also add factory for TransElectro if needed for concurrent operations
            services.AddSingleton<IDbContextFactory<TransElectroDbContext>>(provider =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<TransElectroDbContext>();
                optionsBuilder.UseSqlServer(connectionString, sqlOptions);
                return new PooledDbContextFactory<TransElectroDbContext>(optionsBuilder.Options);
            });

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
    }
    /// <summary>
    /// Extension methods for service registration
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register Employee Sync services
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddEmployeeSyncServices(this IServiceCollection services)
        {
            services.AddScoped<IEmployeeSyncService, EmployeeSyncService>();
            return services;
        }

        /// <summary>
        /// Register all LYNKS services including Employee Sync
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddLynksServices(this IServiceCollection services)
        {
            // Register existing services
            services.AddScoped<ILynksDbService, LynksDbService>();

            // Register new Employee Sync service
            services.AddEmployeeSyncServices();

            return services;
        }
    }
}