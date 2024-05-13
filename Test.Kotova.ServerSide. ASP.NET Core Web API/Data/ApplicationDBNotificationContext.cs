using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class ApplicationDBNotificationContext : DbContext
    {
        public ApplicationDBNotificationContext(DbContextOptions<ApplicationDBNotificationContext> option)
            : base(option) { }
    }
    
}
