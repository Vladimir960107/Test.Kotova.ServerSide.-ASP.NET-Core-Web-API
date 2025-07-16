using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Extensions
{
    /// <summary>
    /// Extension methods for service registration
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// Configure LYNKS Database Context
        /// </summary>
        public static IServiceCollection ConfigureLynksDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "DefaultConnection connection string is not configured. " +
                    "Please add 'ConnectionStrings:DefaultConnection' to your appsettings.json file."
                );
            }

            services.AddDbContext<LynksDbContext>(options =>
                options.UseSqlServer(connectionString));

            return services;
        }

        /// <summary>
        /// Configure TransElectro Database Context (if you have one)
        /// </summary>
        public static IServiceCollection ConfigureTransElectroDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("TransElectroDBConnection");

            if (!string.IsNullOrEmpty(connectionString))
            {
                // Configure shared options for SQL Server
                Action<SqlServerDbContextOptionsBuilder> sqlOptions = sqlBuilder =>
                {
                    sqlBuilder.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                };

                // Add TransElectro DbContext
                services.AddDbContext<TransElectroDbContext>(options =>
                    options.UseSqlServer(connectionString, sqlOptions));

                // Also add factory for TransElectro if needed for concurrent operations
                services.AddSingleton<IDbContextFactory<TransElectroDbContext>>(provider =>
                {
                    var optionsBuilder = new DbContextOptionsBuilder<TransElectroDbContext>();
                    optionsBuilder.UseSqlServer(connectionString, sqlOptions);
                    return new PooledDbContextFactory<TransElectroDbContext>(optionsBuilder.Options);
                });

                Log.Information("TransElectro database context configured successfully");
            }
            else
            {
                Log.Warning("TransElectroDBConnection not found in configuration. TransElectro database will not be available.");
            }

            return services;
        }

        /// <summary>
        /// Configure LYNKS Database Service and related services
        /// </summary>
        public static IServiceCollection ConfigureLynksDbService(this IServiceCollection services)
        {
            // Register existing LYNKS service
            services.AddScoped<ILynksDbService, LynksDbService>();

            // Register Employee Sync service
            services.AddScoped<IEmployeeSyncService, EmployeeSyncService>();

            return services;
        }

        /// <summary>
        /// Register Employee Sync services (separate method for modularity)
        /// </summary>
        public static IServiceCollection AddEmployeeSyncServices(this IServiceCollection services)
        {
            services.AddScoped<IEmployeeSyncService, EmployeeSyncService>();
            return services;
        }

        /// <summary>
        /// Ensure databases are available (for startup checks)
        /// </summary>
        public static async Task EnsureDatabasesAvailableAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            try
            {
                // Check LYNKS database
                var lynksContext = scope.ServiceProvider.GetRequiredService<LynksDbContext>();

                Log.Information("Testing LYNKS database connection...");
                var canConnect = await lynksContext.Database.CanConnectAsync();

                if (!canConnect)
                {
                    throw new InvalidOperationException("Cannot connect to LYNKS database");
                }

                Log.Information("LYNKS database connection successful");

                // Optionally run any pending migrations
                // await lynksContext.Database.MigrateAsync();

                // You can add similar checks for other databases here
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database connection test failed");
                throw new InvalidOperationException($"Database initialization failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Configure all LYNKS services in one method (alternative to individual calls)
        /// </summary>
        public static IServiceCollection AddLynksServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure DbContexts
            services.ConfigureLynksDbContext(configuration);
            services.ConfigureTransElectroDbContext(configuration);

            // Configure Services
            services.ConfigureLynksDbService();

            return services;
        }

        /// <summary>
        /// Alternative method to configure without database check
        /// </summary>
        public static IServiceCollection AddLynksServicesWithoutDbCheck(this IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                services.ConfigureLynksDbContext(configuration);
                services.ConfigureTransElectroDbContext(configuration);
                services.ConfigureLynksDbService();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to configure some services, but continuing");
            }

            return services;
        }
    }
}