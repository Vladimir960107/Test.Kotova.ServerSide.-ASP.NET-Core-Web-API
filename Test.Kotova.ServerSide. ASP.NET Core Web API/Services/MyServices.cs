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


        
    }
}