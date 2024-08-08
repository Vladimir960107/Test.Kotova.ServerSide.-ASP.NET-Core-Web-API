using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class ApplicationDBContextGeneralConstr : ApplicationDBContextBase
    {
        public ApplicationDBContextGeneralConstr(DbContextOptions<ApplicationDBContextGeneralConstr> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entities to use GeneralConstructionDep schema
            modelBuilder.Entity<Instruction>(entity =>
            {
                entity.ToTable("Instructions", "GeneralConstructionDep");
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Department_employees", "GeneralConstructionDep");
            });

            modelBuilder.Entity<FilePath>(entity =>
            {
                entity.ToTable("FilePaths", "GeneralConstructionDep");
            });
        }
    }
}
