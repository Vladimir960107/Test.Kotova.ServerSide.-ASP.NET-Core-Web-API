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
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entities to use UsersSchema
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users", "UsersSchema");
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("Departments", "UsersSchema");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles", "UsersSchema");
            });

            // Add configurations for all your entities as needed
        }
    }
}






/*namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{


    public class ApplicationDbContextUsers : DbContext
    {
        public DbSet<User>? Users { get; set; }

        public DbSet<Department> Departments { get; set; }
        public DbSet<Role> Roles { get; set; }

        public ApplicationDbContextUsers(DbContextOptions<ApplicationDbContextUsers> options)
            : base(options){ }



    }
}*/
