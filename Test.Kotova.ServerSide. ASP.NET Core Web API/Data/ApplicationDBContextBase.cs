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

            modelBuilder.Entity<Employee>()
            .HasKey(e => e.personnel_number);

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
        public List<string> GetTenDigitTableNames()
        {
            var tableNames = new List<string>();
            var connection = this.Database.GetDbConnection();
            try
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT TABLE_NAME 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_TYPE = 'BASE TABLE' 
                        AND TABLE_NAME LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tableNames.Add(reader.GetString(0));
                        }
                    }
                }
            }
            finally
            {
                connection.Close();
            }

            return tableNames;
        }
    }
}