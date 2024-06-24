using System.Collections.Concurrent;
using System.Data.SqlClient;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public class ChiefsManager
    {
        //private Dictionary<int, ChiefSession> sessions = new Dictionary<int, ChiefSession>(); not needed.
        private readonly string _connectionString;
        private ConcurrentDictionary<int, CancellationTokenSource> monitorTasks = new ConcurrentDictionary<int, CancellationTokenSource>();

        public ChiefsManager(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionForUsers");
        }

        public async Task PingChiefAsync(int chiefId)
        {
            if (monitorTasks.TryRemove(chiefId, out var cts))
            {
                // Cancel any existing background task
                cts.Cancel();
            }

            // Start a new task for the chief
            await UpdateChiefSessionAsync(chiefId);
            var newCts = new CancellationTokenSource();
            monitorTasks.TryAdd(chiefId, newCts);
            MonitorChiefStatusInBackground(chiefId, newCts.Token);
        }

        private async void MonitorChiefStatusInBackground(int chiefId, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!await CheckChiefStatus(chiefId))
                    {
                        // Stop monitoring if CheckChiefStatus returns false
                        break;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(60), token);
                }
            }
            catch (TaskCanceledException)
            {
                // Handle task cancellation
                Console.WriteLine($"Monitoring stopped for Chief ID {chiefId}");
            }
            finally
            {
                if (monitorTasks.TryRemove(chiefId, out var _))
                {
                    Console.WriteLine($"Clean up task for Chief ID {chiefId}");
                }
            }
        }


        private async Task UpdateChiefSessionAsync(int chiefId)
        {
            string query = $"UPDATE {DBProcessor.tableName_sql_something} " +
               $"SET {DBProcessor.tableName_sql_isChiefOnline} = @NewValue, " +
               $"{DBProcessor.tableName_sql_lastOnlineSetUTC} = @CurrentDateTime "+
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
            var threshold = DateTime.UtcNow.AddSeconds(-60);

            string query = $"SELECT {DBProcessor.tableName_sql_lastOnlineSetUTC}, {DBProcessor.tableName_sql_isChiefOnline} " +
                           $"FROM {DBProcessor.tableName_sql_something} " +
                           $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ChiefId", chiefId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync()) // Ensures that there is at least one row
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal(DBProcessor.tableName_sql_isChiefOnline)) &&
                                reader.GetBoolean(reader.GetOrdinal(DBProcessor.tableName_sql_isChiefOnline)) == false)
                            {
                                return false; // Returns false if chief_is_online is 0
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal(DBProcessor.tableName_sql_lastOnlineSetUTC)))
                            {
                                DateTime lastOnlineTime = reader.GetDateTime(reader.GetOrdinal(DBProcessor.tableName_sql_lastOnlineSetUTC));
                                if (lastOnlineTime > threshold)
                                {
                                    return true; 
                                }
                                else
                                {
                                    UpdateChiefSessionAsyncToAnotherValue(chiefId); //sets to false chief session cause it is more than 1 minute passed, and chief is probably closed improperly application, so that Chief didn't ping to server about closing.
                                    return false;
                                }
                            }
                        }
                    }
                    return false; // Return false if no rows, or DBNull values for critical fields
                }
            }
        }

        private async Task UpdateChiefSessionAsyncToAnotherValue(int chiefId)
        {
            string query = $"UPDATE {DBProcessor.tableName_sql_something} " +
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
                           $"FROM {DBProcessor.tableName_sql_something} " +
                           $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ChiefId", chiefId);

                    var result = await command.ExecuteScalarAsync();
                    return result != DBNull.Value && (bool)result; // Не понимаю зачем здесь это, разберись позже как будет время. зачем точнее DBNull.Value и && с bool(result)
                }
            }
        }


        /*private async Task DelayAndUpdateAsync(int chiefId)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60));

                await UpdateChiefSessionAsyncToAnotherValue(chiefId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to set to false Chief session for Chief ID {chiefId}: {ex}");
            }
        }*/


        /*public bool IsChiefOnline(int chiefId)
        {
            if (sessions.ContainsKey(chiefId))
            {
                return sessions[chiefId].IsChiefOnline;
            }
            return false;

        }*/

        

        /*public void EndChiefSession(int chiefId)
        {
            if (sessions.ContainsKey(chiefId))
            {
                sessions[chiefId].EndSession();
                sessions.Remove(chiefId);
            }
        }*/

        /*public void PingChief(int chiefId)
        {
            if (sessions.ContainsKey(chiefId))
            {
                sessions[chiefId].ChiefPinged();
            }
            else
            {
                // If chief is not already registered, register and ping
                var session = new ChiefSession(chiefId);
                sessions.Add(chiefId, session);
                session.ChiefPinged();
            }
        }*/

    }
}
