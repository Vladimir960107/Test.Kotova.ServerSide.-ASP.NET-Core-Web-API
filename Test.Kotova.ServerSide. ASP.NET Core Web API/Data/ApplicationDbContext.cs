using DocumentFormat.OpenXml.Spreadsheet;
using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{


    public class ApplicationDbContext : DbContext
    {
        public DbSet<User>? Users { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options){ }



    }
}
