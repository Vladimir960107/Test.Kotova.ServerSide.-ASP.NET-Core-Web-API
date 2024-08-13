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

        
    }
    public static class InstructionExtensions
    {
        public static string? ExtractTenDigitNumber(this string causeOfInstruction)
        {
            var match = Regex.Match(causeOfInstruction, @"Вводный инструктаж для (\d{10})");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}