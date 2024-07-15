using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public abstract class ApplicationDBContextBase : DbContext
    {
        public DbSet<Instruction> Instructions { get; set; }
        public DbSet<Employee> Department_employees { get; set; }
        public DbSet<FilePath> FilePaths { get; set; }

        protected ApplicationDBContextBase(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Instruction>()
                .HasKey(i => i.instruction_id);

            modelBuilder.Entity<Instruction>()
                .Property(i => i.instruction_id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<FilePath>()
                .HasKey(fp => fp.path_id);

            modelBuilder.Entity<FilePath>()
                .Property(fp => fp.path_id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<FilePath>()
                .Property(fp => fp.file_path)
                .HasColumnName("file_path");

            modelBuilder.Entity<FilePath>()
                .Property(fp => fp.instruction_id)
                .HasColumnName("instruction_id");

            modelBuilder.Entity<FilePath>()
                .HasOne(fp => fp.Instruction)
                .WithMany(i => i.FilePaths)
                .HasForeignKey(fp => fp.instruction_id)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}