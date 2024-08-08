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


        public async Task<object> ReadDataFromDynamicTable(string tableName, int? departmentId)
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
                case (5):
                    connectionString = _configuration.GetConnectionString("DefaultConnectionForManagement");
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
            /*string sqlQuery = @$"
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
    t.is_instruction_passed = 0";*/ //DON'T PUT THIS THING INTO ANOTHER FILE BECAUSE OF SECURITY STUFF
            // AND RENAME in SELECT table names into common classes constants!
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
        t.is_instruction_passed = 0";

            string sqlQuery2 = @$"
    SELECT 
        f.instruction_id,
        f.file_path
    FROM 
        FilePaths f
    INNER JOIN 
        [{tableName}] t ON f.instruction_id = t.instruction_id
    WHERE 
        t.is_instruction_passed = 0";

            var result1 = new List<Dictionary<string, object>>();
            var result2 = new List<Dictionary<string, object>>();

            using (var context = new ApplicationDBContextGeneralConstr(optionsBuilder.Options))
            {
                var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var command = conn.CreateCommand())
                {
                    // Execute the first query
                    command.CommandText = sqlQuery;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                            }
                            result1.Add(row);
                        }
                    }

                    // Execute the second query
                    command.CommandText = sqlQuery2;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                            }
                            result2.Add(row);
                        }
                    }
                }
            }

            return new QueryResult
            {
                Result1 = result1,
                Result2 = result2
            };
        }
    }
}