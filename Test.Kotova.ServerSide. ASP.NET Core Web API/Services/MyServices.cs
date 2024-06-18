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
            var temporaryOptionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextUsers>();
            temporaryOptionsBuilder.UseSqlServer(temporaryConnectionString);
            using (var context = new ApplicationDbContextUsers(temporaryOptionsBuilder.Options))
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
            return null;  
        }


        public async Task<List<Dictionary<string, object>>> ReadDataFromDynamicTable(string tableName, int? departmentId)
        {
            string? connectionString = null;
            switch (departmentId) 
            {
                case (1):
                    connectionString = _configuration.GetConnectionString("DefaultConnectionForGeneralConstructionDepartment");
                    break;
                case (2):
                    connectionString = _configuration.GetConnectionString("DefaultConnectionForTechnicalDepartment");
                    break;
                default:
                    break;
            }
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDBContextGeneralConstr>();
            if (connectionString == null)
                throw new ArgumentException("departmentId is invalid, check ReadDataFromDynamicTable");
            optionsBuilder.UseSqlServer(connectionString);

            
            if (!Regex.IsMatch(tableName, @"^\d{10}$")) // Ensure the tableName is a valid 10-digit number
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
            using (var context = new ApplicationDBContextGeneralConstr(optionsBuilder.Options))
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

        internal async Task<int?> GetDepartmentIdToTableName(string userName)
        {
            var temporaryConnectionString = _configuration.GetConnectionString("DefaultConnectionForUsers");
            var temporaryOptionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextUsers>();
            temporaryOptionsBuilder.UseSqlServer(temporaryConnectionString);
            using (var context = new ApplicationDbContextUsers(temporaryOptionsBuilder.Options))
            {
                var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var command = conn.CreateCommand())
                {

                    command.CommandText = $"SELECT {DBProcessor.tableName_sql_departmentId} FROM [{DBProcessor.tableName_pos_users}] WHERE {DBProcessor.columnName_sql_pos_users_username} = @UserName";
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@UserName", SqlDbType.VarChar) { Value = userName });

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                int departmentId = reader.GetInt32(0);
                                return departmentId;
                            }
                                
                        }

                    }
                }
            }
            return null;
        }
    }
}