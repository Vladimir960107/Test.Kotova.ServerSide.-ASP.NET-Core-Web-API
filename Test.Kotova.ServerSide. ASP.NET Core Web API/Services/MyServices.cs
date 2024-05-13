using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using System.Data.SqlClient;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public class MyDataService
    {
        private readonly IConfiguration _configuration;

        public MyDataService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<string?> UserNameToTableName(string userName)
        {
            var temporaryConnectionString = _configuration.GetConnectionString("DefaultConnectionForUsers");
            var temporaryOptionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            temporaryOptionsBuilder.UseSqlServer(temporaryConnectionString);
            var temp = new DBProcessor();
            using (var context = new ApplicationDbContext(temporaryOptionsBuilder.Options))
            {
                var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = $"SELECT {temp.columnName_sql_pos_users_PN} FROM [{temp.tableName_pos_users}] WHERE {temp.columnName_sql_pos_users_username} = @UserName";
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@UserName", SqlDbType.VarChar) { Value = userName });

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetValue(0).ToString();
                        }
                    }
                }
            }
            return null;  // Consider returning null or an appropriate value if no data is found
        }


        public async Task<List<Dictionary<string, object>>> ReadDataFromDynamicTable(string tableName)
        {

            var connectionString = _configuration.GetConnectionString("DefaultConnectionForNotifications");
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDBNotificationContext>();
            optionsBuilder.UseSqlServer(connectionString);

            
            // Ensure the tableName is a valid 10-digit number to prevent SQL Injection
            if (!Regex.IsMatch(tableName, @"^\d{10}$"))
            {
                throw new ArgumentException("Invalid table name");
            }

            using (var context = new ApplicationDBNotificationContext(optionsBuilder.Options))
            {
                var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM [{tableName}]";
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var result = new List<Dictionary<string, object>>();
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                            }
                            result.Add(row);
                        }
                        return result;
                    }
                }
            }
        }
    }
}