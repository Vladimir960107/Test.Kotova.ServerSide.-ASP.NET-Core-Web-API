using DocumentFormat.OpenXml.Spreadsheet;
using Kotova.CommonClasses;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{


    public class ApplicationDbContextUsers : DbContext
    {
        public DbSet<User>? Users { get; set; }

        public DbSet<Department> Departments { get; set; }
        public DbSet<Role> Roles { get; set; }

        public ApplicationDbContextUsers(DbContextOptions<ApplicationDbContextUsers> options)
            : base(options){ }



    }
}
