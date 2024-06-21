using System.Data.SqlClient;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public class ChiefsManager
    {
        private Dictionary<int, ChiefSession> sessions = new Dictionary<int, ChiefSession>();
        private readonly string _connectionString;

        public ChiefsManager(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionForUsers");
        }
        public void PingChief(int chiefId)
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
        }

        public async Task PingChiefAsync(int chiefId)
        {
            await UpdateChiefSessionAsync(chiefId);
        }

        private async Task UpdateChiefSessionAsync(int chiefId)
        {
            string query = $"UPDATE {DBProcessor.tableName_sql_something} " +
               $"SET {DBProcessor.tableName_sql_isChiefOnline} = @NewValue " +
               $"WHERE {DBProcessor.tableName_sql_departmentId} = @ChiefId";
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@NewValue", true);
                    command.Parameters.AddWithValue("@ChiefId", chiefId);

                    command.ExecuteNonQuery();
                }
            }

/*            await Task.Delay(TimeSpan.FromSeconds(60));

            using (var connection = new SqlConnection("YourConnectionString"))
            {
                connection.Open();
                using (var command = new SqlCommand("UPDATE YourTable SET YourColumn = 'NewValue' WHERE Condition", connection))
                {
                    command.ExecuteNonQuery();
                }
            }*/
        }

        public bool IsChiefOnline(int chiefId)
        {
            if (sessions.ContainsKey(chiefId))
            {
                return sessions[chiefId].IsChiefOnline;
            }
            return false;
            
        }

        public void EndChiefSession(int chiefId)
        {
            if (sessions.ContainsKey(chiefId))
            {
                sessions[chiefId].EndSession();
                sessions.Remove(chiefId);
            }
        }
    }
}
