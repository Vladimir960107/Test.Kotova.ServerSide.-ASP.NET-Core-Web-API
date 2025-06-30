using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data
{
    /// <summary>
    /// Database context for ТрансэлектропроектDataBase (TELP DB)
    /// This context manages the connection to the legacy TELP database with actual Russian table names
    /// </summary>
    public class TransElectroDbContext : DbContext
    {
        public TransElectroDbContext(DbContextOptions<TransElectroDbContext> options) : base(options)
        {
        }

        // DbSets for TELP entities
        public DbSet<TelpDepartment> Departments { get; set; }
        public DbSet<TelpPosition> Positions { get; set; }
        public DbSet<TelpEmployee> Employees { get; set; }
        public DbSet<TelpDataSyncLog> DataSyncLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure TelpDepartment (Отделы)
            modelBuilder.Entity<TelpDepartment>(entity =>
            {
                entity.ToTable("Отделы", "dbo");
                entity.HasKey(e => e.Code);
                entity.Property(e => e.Code).HasColumnName("Код");
                entity.Property(e => e.Name).HasColumnName("Наименование").HasMaxLength(255);
                entity.Property(e => e.ShortName).HasColumnName("Кратко").HasMaxLength(255);
                entity.Property(e => e.ChiefId).HasColumnName("Начальник");
                entity.Property(e => e.IsHidden).HasColumnName("Скрыть");
                entity.Property(e => e.Template).HasColumnName("Шаблон");
                entity.Property(e => e.Priority).HasColumnName("Приоритет");
                entity.Property(e => e.DocumentOrder).HasColumnName("Порядок_в_док");
                entity.Property(e => e.GenitiveCase).HasColumnName("Родительный").HasMaxLength(50).IsFixedLength();
                entity.Property(e => e.IsProduction).HasColumnName("Производственный");
                entity.Property(e => e.ServiceNoteNumber).HasColumnName("Номер СЗ");
                entity.Property(e => e.ServiceNotePrefix).HasColumnName("Префикс СЗ").HasMaxLength(20).IsFixedLength();
                entity.Property(e => e.ServiceNotePostfix).HasColumnName("Постфикс СЗ").HasMaxLength(20).IsFixedLength();
                entity.Property(e => e.LetterNumber).HasColumnName("Номер письма");
                entity.Property(e => e.LetterPrefix).HasColumnName("Префикс письма").HasMaxLength(20).IsFixedLength();
                entity.Property(e => e.LetterPostfix).HasColumnName("Постфикс письма").HasMaxLength(20).IsFixedLength();

                // Index on Name for faster lookups
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.ShortName);
            });

            // Configure TelpPosition (Должности)
            modelBuilder.Entity<TelpPosition>(entity =>
            {
                entity.ToTable("Должности", "dbo");
                entity.HasKey(e => e.Code);
                entity.Property(e => e.Code).HasColumnName("Код");
                entity.Property(e => e.Name).HasColumnName("Наименование").HasMaxLength(255);
                entity.Property(e => e.ShortName).HasColumnName("Кратко").HasMaxLength(255);
                entity.Property(e => e.Priority).HasColumnName("Приоритет");
                entity.Property(e => e.IsVisible).HasColumnName("Видимость");
                entity.Property(e => e.ToWhom).HasColumnName("Кому").HasMaxLength(255);

                // Indexes
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Priority);
            });

            // Configure TelpEmployee (Сотрудники)
            modelBuilder.Entity<TelpEmployee>(entity =>
            {
                entity.ToTable("Сотрудники", "dbo");
                entity.HasKey(e => e.Code);
                entity.Property(e => e.Code).HasColumnName("Код");
                entity.Property(e => e.FullName).HasColumnName("ФИО").HasMaxLength(255).IsRequired();
                entity.Property(e => e.DepartmentCode).HasColumnName("Отдел").HasDefaultValue(0);
                entity.Property(e => e.PositionCode).HasColumnName("Должность").HasDefaultValue(0);
                entity.Property(e => e.ComputerName).HasColumnName("Имя_Компьютера").HasMaxLength(50);
                entity.Property(e => e.HasAccess).HasColumnName("Доступ");
                entity.Property(e => e.Email).HasColumnName("e-mail").HasMaxLength(50);
                entity.Property(e => e.UserName).HasColumnName("User_Name").HasMaxLength(50);
                entity.Property(e => e.IsHidden).HasColumnName("Hide");
                entity.Property(e => e.Room2).HasColumnName("Комната2").HasMaxLength(10).IsFixedLength();
                entity.Property(e => e.ReceiveMessages).HasColumnName("Сообщения");
                entity.Property(e => e.AccessRights).HasColumnName("Права_доступа");
                entity.Property(e => e.Room).HasColumnName("Комната").HasMaxLength(30).IsFixedLength();
                entity.Property(e => e.GroupId).HasColumnName("Группа");
                entity.Property(e => e.PersonnelNumber).HasColumnName("Табельный_номер").HasMaxLength(10).IsFixedLength();

                // Relationships
                entity.HasOne(e => e.Department)
                      .WithMany(d => d.Employees)
                      .HasForeignKey(e => e.DepartmentCode)
                      .HasPrincipalKey(d => d.Code)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Position)
                      .WithMany(p => p.Employees)
                      .HasForeignKey(e => e.PositionCode)
                      .HasPrincipalKey(p => p.Code)
                      .OnDelete(DeleteBehavior.SetNull);

                // Indexes for fast lookups
                entity.HasIndex(e => e.PersonnelNumber);
                entity.HasIndex(e => e.FullName);
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.DepartmentCode);
                entity.HasIndex(e => e.PositionCode);
                entity.HasIndex(e => e.IsHidden);
            });

            // Configure TelpDataSyncLog (our custom sync log table)
            modelBuilder.Entity<TelpDataSyncLog>(entity =>
            {
                entity.ToTable("DataSyncLog", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SyncType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.EntityId).HasMaxLength(100);
                entity.Property(e => e.Operation).IsRequired().HasMaxLength(50);
                entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
                entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");
                entity.Property(e => e.SyncedBy).HasMaxLength(255);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.SyncDate).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsSuccessful).HasDefaultValue(true);

                // Indexes for reporting and monitoring
                entity.HasIndex(e => e.SyncType);
                entity.HasIndex(e => e.SyncDate);
                entity.HasIndex(e => e.IsSuccessful);
                entity.HasIndex(e => new { e.SyncType, e.EntityId });
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // This will be overridden by DI configuration, but useful for migrations
                optionsBuilder.UseSqlServer("DefaultConnection");
            }

            // Enable sensitive data logging in development
#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
#endif
        }
    }
}