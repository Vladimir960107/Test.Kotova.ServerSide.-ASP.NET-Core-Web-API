using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Extensions
{
    public static class ServiceExtensions
    {
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

        public static IServiceCollection ConfigureLynksDbService(
            this IServiceCollection services)
        {
            services.AddScoped<ILynksDbService, LynksDbService>();

            return services;
        }
    }
}