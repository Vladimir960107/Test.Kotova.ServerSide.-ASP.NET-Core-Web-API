using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public class ChiefsStatusChecker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _connectionString;

        public ChiefsStatusChecker(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _connectionString = configuration.GetConnectionString("DefaultConnectionForUsers");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAndSetOfflineStatusAsync();
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        private async Task CheckAndSetOfflineStatusAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var currentTime = DateTime.UtcNow;

                using (var connection = new SqlConnection(_connectionString))
                {
                    string query = $"UPDATE {DBProcessor.tableName_sql_departments_NameDB} " +
                                   $"SET {DBProcessor.tableName_sql_isChiefOnline} = 0 " +
                                   $"WHERE {DBProcessor.tableName_sql_isChiefOnline} = 1 AND " +
                                   $"{DBProcessor.tableName_sql_lastOnlineSetUTC} < @ThresholdTime";
                    var command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ThresholdTime", currentTime.AddSeconds(-60));

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
