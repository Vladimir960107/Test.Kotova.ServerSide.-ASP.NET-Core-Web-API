using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class ApplicationDBInstructionsContext : DbContext
    {
        public DbSet<Instruction> Instructions { get; set; }

        public DbSet<Employee> Department_employees { get; set; }  

        public ApplicationDBInstructionsContext(DbContextOptions<ApplicationDBInstructionsContext> option)
            : base(option) {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Instruction>()
                .HasKey(i => i.instruction_id);  // Setting the primary key if not already set

            modelBuilder.Entity<Instruction>()
                .Property(i => i.instruction_id)
                .ValueGeneratedOnAdd();  // Configures the value to be generated on add
        }
    }
    
}
