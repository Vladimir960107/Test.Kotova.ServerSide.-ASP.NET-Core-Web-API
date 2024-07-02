using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API;

public class GracefulShutdownService : IHostedService
{
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IConfiguration _configuration;

    public GracefulShutdownService(ILogger<GracefulShutdownService> logger, IHostApplicationLifetime appLifetime, IConfiguration configuration)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStopping.Register(OnShutdown);
        return Task.CompletedTask;
    }

    private void OnShutdown()
    {
        // Perform your SQL Server update here
        try
        {
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnectionForUsers")))
            {
                connection.Open();

                var command = new SqlCommand(
                    $"UPDATE [{DBProcessor.tableName_sql_departments_NameDB.Split(".")[1]}] " + //Here it takes "departments" instead of "dbo.departments"
                    $"SET [{DBProcessor.tableName_sql_isChiefOnline}] = 0", //TURNS ALL CHIEF TO OFFLINE IN DB :)
                    connection);
                command.ExecuteNonQuery();
            }

            _logger.LogInformation("Database update completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating the database.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}