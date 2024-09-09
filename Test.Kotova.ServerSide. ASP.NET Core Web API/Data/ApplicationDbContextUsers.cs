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
        public DbSet<TaskForUser> Tasks { get; set; }

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

            modelBuilder.Entity<TaskForUser>(entity =>
            {
                entity.ToTable("Tasks", "UsersSchema"); // Specify the schema

                entity.Property(t => t.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(t => t.Status)
                    .HasDefaultValue("Не назначено");

                entity.Property(t => t.IsDeleted)
                    .HasDefaultValue(false);
            });
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
