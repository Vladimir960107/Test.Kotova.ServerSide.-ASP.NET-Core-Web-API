using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class ApplicationDBContextTechnicalDepartment : ApplicationDBContextBase
    {
        public ApplicationDBContextTechnicalDepartment(DbContextOptions<ApplicationDBContextTechnicalDepartment> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entities to use TechnicalDep schema
            modelBuilder.Entity<Instruction>(entity =>
            {
                entity.ToTable("Instructions", "TechnicalDep");
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Department_employees", "TechnicalDep");
            });

            modelBuilder.Entity<FilePath>(entity =>
            {
                entity.ToTable("FilePaths", "TechnicalDep");
            });
        }
    }
}
