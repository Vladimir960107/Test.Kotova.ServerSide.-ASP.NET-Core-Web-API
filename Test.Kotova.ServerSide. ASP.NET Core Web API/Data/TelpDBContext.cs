using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    public class TelpDbContext : DbContext
    {
        public DbSet<TelpEmployee> Employees { get; set; }
        public DbSet<TelpDepartment> Departments { get; set; }
        public DbSet<TelpPosition> Positions { get; set; }

        public TelpDbContext(DbContextOptions<TelpDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TelpEmployee>(entity =>
            {
                entity.ToTable("Сотрудники", "dbo");
                entity.HasKey(e => e.Id);

                // Map English property names to Russian column names
                entity.Property(e => e.Id).HasColumnName("Код");
                entity.Property(e => e.FullName).HasColumnName("ФИО");
                entity.Property(e => e.DepartmentId).HasColumnName("Отдел");
                entity.Property(e => e.PositionId).HasColumnName("Должность");
                entity.Property(e => e.Email).HasColumnName("e-mail");
                entity.Property(e => e.PersonnelNumber).HasColumnName("Табельный_номер");

                entity.HasOne(d => d.Department)
                    .WithMany(p => p.Employees)
                    .HasForeignKey(d => d.DepartmentId);

                entity.HasOne(d => d.Position)
                    .WithMany(p => p.Employees)
                    .HasForeignKey(d => d.PositionId);
            });

            modelBuilder.Entity<TelpDepartment>(entity =>
            {
                entity.ToTable("Отделы", "dbo");
                entity.HasKey(e => e.Id);

                // Map English property names to Russian column names
                entity.Property(e => e.Id).HasColumnName("Код");
                entity.Property(e => e.Name).HasColumnName("Наименование");
            });

            modelBuilder.Entity<TelpPosition>(entity =>
            {
                entity.ToTable("Должности", "dbo");
                entity.HasKey(e => e.Id);

                // Map English property names to Russian column names
                entity.Property(e => e.Id).HasColumnName("Код");
                entity.Property(e => e.Name).HasColumnName("Наименование");
            });
        }
    }
}