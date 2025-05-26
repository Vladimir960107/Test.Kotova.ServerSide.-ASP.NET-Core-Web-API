using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class LynksDbContext : DbContext
    {
        public LynksDbContext(DbContextOptions<LynksDbContext> options) : base(options)
        {
        }

        public DbSet<Department> Departments { get; set; }
        public DbSet<Personnel> Personnel { get; set; }
        public DbSet<EmployeeByDepartment> EmployeesByDepartment { get; set; }
        public DbSet<Instruction> Instructions { get; set; }
        public DbSet<FilePathForInstruction> FilePathsForInstructions { get; set; }
        public DbSet<InstructionType> InstructionTypes { get; set; }
        public DbSet<InstructionStatus> InstructionStatuses { get; set; }
        public DbSet<NormativeInstructionName> NormativeInstructionNames { get; set; }
        public DbSet<InstructionStatusToNormativeInstrName> InstructionStatusToNormativeInstrNames { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Models.Task> Tasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure composite key for EmployeeByDepartment
            modelBuilder.Entity<EmployeeByDepartment>()
                .HasKey(e => new { e.personnel_id, e.department_id });

            // Configure unique constraint for cause_of_instruction in Instructions
            modelBuilder.Entity<Instruction>()
                .HasIndex(i => i.cause_of_instruction)
                .IsUnique();

            modelBuilder.Entity<Instruction>()
                .Property(i => i.is_passed_by_chief_unplanned_instr)
                .HasDefaultValue(false);

            // Configure unique constraint for personnel_number in Personnel
            modelBuilder.Entity<Personnel>()
                .HasIndex(p => p.personnel_number)
                .IsUnique();

            // Configure unique constraint for normative_instruction_name in NormativeInstructionName
            modelBuilder.Entity<NormativeInstructionName>()
                .HasIndex(n => n.normative_instruction_name)
                .IsUnique();

            // Configure unique constraint for combination of personnel_id and instruction_id in InstructionStatus
            modelBuilder.Entity<InstructionStatus>()
                .HasIndex(i => new { i.personnel_id, i.instruction_id })
                .IsUnique();

            // Configure default values
            modelBuilder.Entity<Department>()
                .Property(d => d.is_chief_online)
                .HasDefaultValue(false);

            modelBuilder.Entity<Department>()
                .Property(d => d.code_number_TELP_DB)
                .HasDefaultValue((byte)0);

            modelBuilder.Entity<EmployeeByDepartment>()
                .Property(e => e.department_id)
                .HasDefaultValue(-1);

            modelBuilder.Entity<NormativeInstructionName>()
                .Property(n => n.created_at)
                .HasDefaultValueSql("GETDATE()");

            /*modelBuilder.Entity<Task>() //TASK IS NOT IMPLEMENTED YET
                .Property(t => t.created_at)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Task>()
                .Property(t => t.status)
                .HasDefaultValue("Not assigned to");

            modelBuilder.Entity<Task>()
                .Property(t => t.is_deleted)
                .HasDefaultValue(false);*/
        }
    }
}
