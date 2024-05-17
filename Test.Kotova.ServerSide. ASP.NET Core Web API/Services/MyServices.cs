using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using System.Data.SqlClient;
using System.Data;
using Microsoft.Data.SqlClient;
using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public class MyDataService
    {
        public readonly IConfiguration _configuration;

        public MyDataService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<string?> UserNameToTableName(string userName)
        {
            var temporaryConnectionString = _configuration.GetConnectionString("DefaultConnectionForUsers");
            var temporaryOptionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            temporaryOptionsBuilder.UseSqlServer(temporaryConnectionString);
            using (var context = new ApplicationDbContext(temporaryOptionsBuilder.Options))
            {
                var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var command = conn.CreateCommand())
                { 
                    
                    command.CommandText = $"SELECT {DBProcessor.columnName_sql_pos_users_PN} FROM [{DBProcessor.tableName_pos_users}] WHERE {DBProcessor.columnName_sql_pos_users_username} = @UserName";
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
            string sqlQuery = @$"
SELECT 
    t.ID,
    t.instruction_id,
    t.when_was_send_to_user,
    i.path_to_instruction,
    i.cause_of_instruction,
    i.type_of_instruction
FROM 
    [{tableName}] t
INNER JOIN 
    Instructions i ON t.instruction_id = i.instruction_id
WHERE 
    t.is_instruction_passed = 0"; //DON'T PUT THIS THING INTO ANOTHER FILE BECAUSE OF SECURITY STUFF
            // AND RENAME in SELECT table names into common classes constants!
            using (var context = new ApplicationDBNotificationContext(optionsBuilder.Options))
            {
                var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = sqlQuery;

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