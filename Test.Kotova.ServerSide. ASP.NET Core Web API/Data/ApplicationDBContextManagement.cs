using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class ApplicationDBContextManagement : ApplicationDBContextBase
    {
        public ApplicationDBContextManagement(DbContextOptions<ApplicationDBContextManagement> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entities to use Management schema
            modelBuilder.Entity<Instruction>(entity =>
            {
                entity.ToTable("Instructions", "Management");
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Department_employees", "Management");
            });

            modelBuilder.Entity<FilePath>(entity =>
            {
                entity.ToTable("FilePaths", "Management");
            });
        }
    }
}
