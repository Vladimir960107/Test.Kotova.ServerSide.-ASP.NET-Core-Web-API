using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public abstract class ApplicationDBContextBase : DbContext
    {
        public DbSet<Instruction> Instructions { get; set; }
        public DbSet<Employee> Department_employees { get; set; }
        public DbSet<FilePath> FilePaths { get; set; }
        public DbSet<InstructionExportInstance> InstructionExportInstances { get; set; }

        protected ApplicationDBContextBase(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
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

            modelBuilder.Entity<DynamicEmployeeInstruction>().HasNoKey();
            modelBuilder.Entity<InstructionExportInstance>().HasNoKey();
            base.OnModelCreating(modelBuilder);
        }
        public List<string> GetTenDigitTableNames(string schemaName = null)
        {
            var tableNames = new List<string>();
            var connection = this.Database.GetDbConnection();
            try
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                SELECT TABLE_SCHEMA, TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE' 
                AND TABLE_NAME LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'" +
                        (schemaName != null ? " AND TABLE_SCHEMA = @SchemaName" : string.Empty);

                    if (schemaName != null)
                    {
                        var schemaParameter = command.CreateParameter();
                        schemaParameter.ParameterName = "@SchemaName";
                        schemaParameter.Value = schemaName;
                        command.Parameters.Add(schemaParameter);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string fullTableName = $"{reader.GetString(0)}.{reader.GetString(1)}"; // Schema.TableName
                            tableNames.Add(fullTableName);
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