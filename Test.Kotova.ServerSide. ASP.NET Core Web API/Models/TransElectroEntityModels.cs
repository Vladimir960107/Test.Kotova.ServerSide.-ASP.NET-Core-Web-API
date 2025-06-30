using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models
{
    /// <summary>
    /// Entity models for ТрансэлектропроектDataBase
    /// These models match the actual structure of the TELP database
    /// </summary>

    // Отделы (Departments) table
    [Table("Отделы", Schema = "dbo")]
    public class TelpDepartment
    {
        [Key]
        [Column("Код")]
        public int Code { get; set; }

        [Column("Наименование")]
        [StringLength(255)]
        public string? Name { get; set; }

        [Column("Кратко")]
        [StringLength(255)]
        public string? ShortName { get; set; }

        [Column("Начальник")]
        public int? ChiefId { get; set; }

        [Column("Скрыть")]
        public bool? IsHidden { get; set; }

        [Column("Шаблон")]
        public int? Template { get; set; }

        [Column("Приоритет")]
        public int? Priority { get; set; }

        [Column("Порядок_в_док")]
        public int? DocumentOrder { get; set; }

        [Column("Родительный")]
        [StringLength(50)]
        public string? GenitiveCase { get; set; }

        [Column("Производственный")]
        public bool? IsProduction { get; set; }

        [Column("Номер СЗ")]
        public int? ServiceNoteNumber { get; set; }

        [Column("Префикс СЗ")]
        [StringLength(20)]
        public string? ServiceNotePrefix { get; set; }

        [Column("Постфикс СЗ")]
        [StringLength(20)]
        public string? ServiceNotePostfix { get; set; }

        [Column("Номер письма")]
        public int? LetterNumber { get; set; }

        [Column("Префикс письма")]
        [StringLength(20)]
        public string? LetterPrefix { get; set; }

        [Column("Постфикс письма")]
        [StringLength(20)]
        public string? LetterPostfix { get; set; }

        // Navigation properties
        public virtual ICollection<TelpEmployee> Employees { get; set; } = new List<TelpEmployee>();
    }

    // Должности (Positions) table
    [Table("Должности", Schema = "dbo")]
    public class TelpPosition
    {
        [Key]
        [Column("Код")]
        public int Code { get; set; }

        [Column("Наименование")]
        [StringLength(255)]
        public string? Name { get; set; }

        [Column("Кратко")]
        [StringLength(255)]
        public string? ShortName { get; set; }

        [Column("Приоритет")]
        public int? Priority { get; set; }

        [Column("Видимость")]
        public bool? IsVisible { get; set; }

        [Column("Кому")]
        [StringLength(255)]
        public string? ToWhom { get; set; }

        // Navigation properties
        public virtual ICollection<TelpEmployee> Employees { get; set; } = new List<TelpEmployee>();
    }

    // Сотрудники (Employees) table
    [Table("Сотрудники", Schema = "dbo")]
    public class TelpEmployee
    {
        [Key]
        [Column("Код")]
        public int Code { get; set; }

        [Required]
        [Column("ФИО")]
        [StringLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Column("Отдел")]
        public int? DepartmentCode { get; set; }

        [Column("Должность")]
        public int? PositionCode { get; set; }

        [Column("Имя_Компьютера")]
        [StringLength(50)]
        public string? ComputerName { get; set; }

        [Column("Доступ")]
        public bool? HasAccess { get; set; }

        [Column("e-mail")]
        [StringLength(50)]
        public string? Email { get; set; }

        [Column("User_Name")]
        [StringLength(50)]
        public string? UserName { get; set; }

        [Column("Hide")]
        public bool? IsHidden { get; set; }

        [Column("Комната2")]
        [StringLength(10)]
        public string? Room2 { get; set; }

        [Column("Сообщения")]
        public bool? ReceiveMessages { get; set; }

        [Column("Права_доступа")]
        public int? AccessRights { get; set; }

        [Column("Комната")]
        [StringLength(30)]
        public string? Room { get; set; }

        [Column("Группа")]
        public int? GroupId { get; set; }

        [Column("Табельный_номер")]
        [StringLength(10)]
        public string? PersonnelNumber { get; set; }

        // Navigation properties
        [ForeignKey("DepartmentCode")]
        public virtual TelpDepartment? Department { get; set; }

        [ForeignKey("PositionCode")]
        public virtual TelpPosition? Position { get; set; }
    }

    // Additional entity for tracking data synchronization (custom table for our needs)
    [Table("DataSyncLog", Schema = "dbo")]
    public class TelpDataSyncLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SyncType { get; set; } = string.Empty; // Employee, Department, Position

        [StringLength(100)]
        public string? EntityId { get; set; }

        [Required]
        [StringLength(50)]
        public string Operation { get; set; } = string.Empty; // Create, Update, Delete

        [Column(TypeName = "nvarchar(max)")]
        public string? OldValues { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? NewValues { get; set; }

        public DateTime SyncDate { get; set; } = DateTime.UtcNow;

        [StringLength(255)]
        public string? SyncedBy { get; set; }

        public bool IsSuccessful { get; set; } = true;

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }
    }
}