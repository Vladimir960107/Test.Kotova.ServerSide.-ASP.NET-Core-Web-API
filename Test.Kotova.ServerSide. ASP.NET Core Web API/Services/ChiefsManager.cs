using System;
using System.Data.SqlClient;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Configuration;



namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public class ChiefsManager
    {
        //private Dictionary<int, ChiefSession> sessions = new Dictionary<int, ChiefSession>(); not needed.
        private readonly string _connectionString;
        private ConcurrentDictionary<int, (CancellationTokenSource Cts, Task MonitoringTask)> monitorTasks = new ConcurrentDictionary<int, (CancellationTokenSource Cts, Task MonitoringTask)>();

        public ChiefsManager(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionForUsers");
        }

        public async Task PingChiefAsync(int chiefId) // ЗДЕСЬ СОБСТВЕННО СЮДА ПИНГУЕТ С КЛИЕНТСКОГО ПРИЛОЖЕНИЯ КАЖДЫЕ 30 СЕКУНД. 
        {
            // Check if there is an existing task and whether it is still running.
            if (monitorTasks.TryGetValue(chiefId, out var existingTaskInfo))
            {
                // Check if the task has completed or has been cancelled.
                if (existingTaskInfo.MonitoringTask.IsCompleted || existingTaskInfo.MonitoringTask.IsCanceled)
                {
                    // If the task is no longer active, remove it from the dictionary.
                    if (monitorTasks.TryRemove(chiefId, out var removedTaskInfo))
                    {
                        Console.WriteLine($"Completed monitoring task for Chief ID {chiefId} removed.");
                    }
                }
                else
                {
                    // If the task is still active, simply return and do nothing.
                    Console.WriteLine($"Monitoring task for Chief ID {chiefId} is still active. No new task started.");
                    return;
                }
            }

            // Proceed to update the session in the database.
            await UpdateChiefSessionAsync(chiefId);

            // If no active monitoring task exists, start a new one.
            var newCts = new CancellationTokenSource();
            var newTask = MonitorChiefStatusInBackground(chiefId, newCts.Token);
            monitorTasks.TryAdd(chiefId, (newCts, newTask));
            Console.WriteLine($"New monitoring task started for Chief ID {chiefId}.");
        }

        private async Task MonitorChiefStatusInBackground(int chiefId, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!await CheckChiefStatus(chiefId))
                    {
                        break;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(60), token);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"Monitoring stopped for Chief ID {chiefId}");
            }
            finally
            {
                if (monitorTasks.TryRemove(chiefId, out var _))
                {
                    Console.WriteLine($"Clean up task for Chief ID {chiefId} completed."); //ВАЖНО! TODO: Не работает так как задумано.
                }
            }
        }


        private async Task UpdateChiefSessionAsync(int chiefId)
        {
            string query = $"UPDATE {DBProcessor.tableName_sql_departments_NameDB} " +
               $"SET {DBProcessor.tableName_sql_isChiefOnline} = @NewValue, " +
               $"{DBProcessor.tableName_sql_lastOnlineSetUTC} = @CurrentDateTime " +
               $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@NewValue", true);
                    command.Parameters.AddWithValue("@CurrentDateTime", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@ChiefId", chiefId);

                    command.ExecuteNonQuery();
                }
            }
        }

        internal async Task PingOfflineChiefAsync(int chiefId)
        {
            await UpdateChiefSessionAsyncToAnotherValue(chiefId);
        }

        public async Task<bool> CheckChiefStatus(int chiefId)
        {
            var threshold = DateTime.UtcNow.AddSeconds(-60);  // Adjust based on your session timeout needs

            string query = $"SELECT {DBProcessor.tableName_sql_lastOnlineSetUTC}, {DBProcessor.tableName_sql_isChiefOnline} " +
                           $"FROM {DBProcessor.tableName_sql_departments_NameDB} " +
                           $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ChiefId", chiefId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            DateTime lastOnlineTime = reader.GetDateTime(reader.GetOrdinal(DBProcessor.tableName_sql_lastOnlineSetUTC));
                            bool isChiefOnline = reader.GetBoolean(reader.GetOrdinal(DBProcessor.tableName_sql_isChiefOnline));
                            if (lastOnlineTime <= threshold || !isChiefOnline)
                            {
                                UpdateChiefSessionAsyncToAnotherValue(chiefId);  // Logic here should ensure the session is set appropriately
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;  // No session exists, potentially start a new one
        }

        private async Task UpdateChiefSessionAsyncToAnotherValue(int chiefId)
        {
            string query = $"UPDATE {DBProcessor.tableName_sql_departments_NameDB} " +
                           $"SET {DBProcessor.tableName_sql_isChiefOnline} = @NewValue " +
                           $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@NewValue", false);
                    command.Parameters.AddWithValue("@ChiefId", chiefId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<bool> IsChiefOnlineAsync(int chiefId)
        {
            string query = $"SELECT {DBProcessor.tableName_sql_isChiefOnline} " +
                           $"FROM {DBProcessor.tableName_sql_departments_NameDB} " +
                           $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ChiefId", chiefId);

                    var result = await command.ExecuteScalarAsync();
                    return result != DBNull.Value && (bool)result; // Не до конца понимаю зачем здесь это, разберись позже как будет время. зачем точнее DBNull.Value и && с bool(result)
                }
            }
        }
    }
}


/*namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public class ChiefsManager //TODO: НИФИГА НЕ РАБОТАЕТ! ПЕРЕИСПРАВЛЯЙ :)
    {
        private readonly string _connectionString;
        private readonly ConcurrentDictionary<int, System.Timers.Timer> _timers;

        public ChiefsManager(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionForUsers");
            _timers = new ConcurrentDictionary<int, System.Timers.Timer>();
        }

        public async Task PingChiefAsync(int chiefId)
        {
            await UpdateChiefStatusAsync(chiefId, true);
            ScheduleOfflineCheck(chiefId);
        }

        private void ScheduleOfflineCheck(int chiefId)
        {
            if (_timers.TryGetValue(chiefId, out var existingTimer))
            {
                existingTimer.Stop();
                existingTimer.Dispose();
            }

            var timer = new System.Timers.Timer(60000); // 60 seconds
            timer.Elapsed += async (sender, e) => await TimerElapsedAsync(chiefId);
            timer.AutoReset = false;
            timer.Start();

            _timers[chiefId] = timer;
        }

        private async Task TimerElapsedAsync(int chiefId)
        {
            await CheckAndSetOfflineStatusAsync(chiefId);
            _timers.TryRemove(chiefId, out _); // Remove the timer once the check is done
        }

        public async Task CheckAndSetOfflineStatusAsync(int chiefId)
        {
            Console.WriteLine($"Setting chief offline status: {chiefId}");
            using (var connection = new SqlConnection(_connectionString))
            {
                var currentTime = DateTime.UtcNow;
                string query = $"SELECT {DBProcessor.tableName_sql_lastOnlineSetUTC} " +
                               $"FROM {DBProcessor.tableName_sql_departments_NameDB} " +
                               $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ChiefId", chiefId);

                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                {
                    var lastPingTime = (DateTime)result;
                    if ((currentTime - lastPingTime).TotalSeconds >= 60)
                    {
                        await UpdateChiefStatusAsync(chiefId, false);
                    }
                }
            }
        }

        private async Task UpdateChiefStatusAsync(int chiefId, bool isOnline)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = $"UPDATE {DBProcessor.tableName_sql_departments_NameDB} " +
                               $"SET {DBProcessor.tableName_sql_isChiefOnline} = @NewValue, " +
                               $"{DBProcessor.tableName_sql_lastOnlineSetUTC} = @LastPingTime " +
                               $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@NewValue", isOnline);
                command.Parameters.AddWithValue("@LastPingTime", DateTime.UtcNow);
                command.Parameters.AddWithValue("@ChiefId", chiefId);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> IsChiefOnlineAsync(int chiefId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = $"SELECT {DBProcessor.tableName_sql_isChiefOnline} " +
                            $"FROM {DBProcessor.tableName_sql_departments_NameDB} " +
                            $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ChiefId", chiefId);

                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                {
                    return (bool)result;
                }
                else
                {
                    return false; // or handle as appropriate
                }
            }
        }
    }
}*/