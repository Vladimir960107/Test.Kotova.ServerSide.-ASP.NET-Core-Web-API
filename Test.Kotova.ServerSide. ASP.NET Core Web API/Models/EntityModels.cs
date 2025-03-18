using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models
{
    // Department entity
    [Table("departments_names", Schema = "Management")]
    public class Department
    {
        [Key]
        public int department_id { get; set; }

        [Required]
        [StringLength(255)]
        public string department_name { get; set; }

        public bool is_chief_online { get; set; }

        public DateTime? last_online_set_UTC { get; set; }

        public byte code_number_TELP_DB { get; set; }

        // Navigation properties
        public virtual ICollection<EmployeeByDepartment> Employees { get; set; }
        public virtual ICollection<Instruction> Instructions { get; set; }
        public virtual ICollection<InstructionStatus> InstructionStatuses { get; set; }
        public virtual ICollection<User> Users { get; set; }
        public virtual ICollection<Task> Tasks { get; set; }
    }

    // Personnel entity
    [Table("personnel", Schema = "Management")]
    public class Personnel
    {
        [Key]
        public int personnel_id { get; set; }

        [Required]
        [StringLength(10)]
        public string personnel_number { get; set; }

        // Navigation properties
        public virtual ICollection<EmployeeByDepartment> EmployeesByDepartment { get; set; }
        public virtual ICollection<InstructionStatus> InstructionStatuses { get; set; }
        public virtual ICollection<User> Users { get; set; }
    }

    // Employee by Department entity
    [Table("employees_by_department", Schema = "Management")]
    public class EmployeeByDepartment
    {
        [Key, Column(Order = 0)]
        public int personnel_id { get; set; }

        [Key, Column(Order = 1)]
        public int department_id { get; set; }

        [Required]
        [StringLength(255)]
        public string full_name { get; set; }

        [Required]
        [StringLength(255)]
        public string job_position { get; set; }

        [StringLength(255)]
        public string group { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime birth_date { get; set; }

        public byte gender { get; set; }

        public bool is_driver { get; set; }

        public bool is_working_in_department { get; set; }

        // Navigation properties
        [ForeignKey("personnel_id")]
        public virtual Personnel Personnel { get; set; }

        [ForeignKey("department_id")]
        public virtual Department Department { get; set; }
    }

    // Instruction entity
    [Table("instructions", Schema = "Management")]
    public class Instruction
    {
        [Key]
        public int instruction_id { get; set; }

        public int department_id { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime begin_date { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime end_date { get; set; }

        [Required]
        [StringLength(500)]
        public string cause_of_instruction { get; set; }

        public byte type_of_instruction { get; set; }

        public bool is_assigned_to_people { get; set; }

        public bool is_passed_by_everyone { get; set; }

        // Navigation properties
        [ForeignKey("department_id")]
        public virtual Department Department { get; set; }

        [ForeignKey("type_of_instruction")]
        public virtual InstructionType InstructionType { get; set; }

        public virtual ICollection<FilePathForInstruction> FilePaths { get; set; }
        public virtual ICollection<InstructionStatus> InstructionStatuses { get; set; }
    }

    // File Path for Instruction entity
    [Table("filepaths_for_instructions", Schema = "Management")]
    public class FilePathForInstruction
    {
        [Key]
        public int path_id { get; set; }

        public int instruction_id { get; set; }

        [Required]
        [StringLength(255)]
        public string file_path { get; set; }

        [Required]
        [StringLength(255)]
        public string instruction_name { get; set; }

        // Navigation properties
        [ForeignKey("instruction_id")]
        public virtual Instruction Instruction { get; set; }
    }

    // Instruction Type entity
    [Table("name_of_instruction_by_id", Schema = "Management")]
    public class InstructionType
    {
        [Key]
        public byte type_of_instruction { get; set; }

        [Required]
        [StringLength(100)]
        public string name_of_type_instruction { get; set; }

        // Navigation properties
        public virtual ICollection<Instruction> Instructions { get; set; }
    }

    // Instruction Status entity
    [Table("instructions_status", Schema = "Management")]
    public class InstructionStatus
    {
        [Key]
        public int id { get; set; }

        public int personnel_id { get; set; }

        public int department_id { get; set; }

        public int instruction_id { get; set; }

        public bool is_instruction_passed { get; set; }

        public DateTime? date_when_passed { get; set; }

        public DateTime? date_when_passed_UTC { get; set; }

        public DateTime? when_was_sent_to_user { get; set; }

        public DateTime? when_was_sent_to_user_UTC { get; set; }

        public int was_signed_by_personnel_id { get; set; }

        // Navigation properties
        [ForeignKey("personnel_id")]
        public virtual Personnel Personnel { get; set; }

        [ForeignKey("department_id")]
        public virtual Department Department { get; set; }

        [ForeignKey("instruction_id")]
        public virtual Instruction Instruction { get; set; }

        public virtual ICollection<InstructionStatusToNormativeInstrName> NormativeInstructions { get; set; }
    }

    // Normative Instructions Names entity
    [Table("normative_instructions_names", Schema = "Management")]
    public class NormativeInstructionName
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(255)]
        public string normative_instruction_name { get; set; }

        public string url { get; set; }

        public DateTime created_at { get; set; }

        // Navigation properties
        public virtual ICollection<InstructionStatusToNormativeInstrName> InstructionStatuses { get; set; }
    }

    // InstructionStatus to NormativeInstructionName entity (many-to-many relationship)
    [Table("instruction_status_to_normative_instr_names", Schema = "Management")]
    public class InstructionStatusToNormativeInstrName
    {
        [Key]
        public int id { get; set; }

        public int instruction_status_id { get; set; }

        public int normative_instruction_name_id { get; set; }

        // Navigation properties
        [ForeignKey("instruction_status_id")]
        public virtual InstructionStatus InstructionStatus { get; set; }

        [ForeignKey("normative_instruction_name_id")]
        public virtual NormativeInstructionName NormativeInstructionName { get; set; }
    }

    // Role Names entity
    [Table("role_names", Schema = "Management")]
    public class Role
    {
        [Key]
        public int role_id { get; set; }

        [Required]
        [StringLength(100)]
        public string role_type { get; set; }

        [Required]
        [StringLength(100)]
        public string role_name_russian { get; set; }

        // Navigation properties
        public virtual ICollection<User> Users { get; set; }
    }

    // User entity
    [Table("users", Schema = "Management")]
    public class User
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(50)]
        public string username { get; set; }

        [Required]
        [StringLength(60)]
        public string password_hash { get; set; }

        public int user_role_id { get; set; }

        public int personnel_id { get; set; }

        [StringLength(70)]
        public string current_email { get; set; }

        public int department_id { get; set; }

        [StringLength(50)]
        public string desk_number { get; set; }

        // Navigation properties
        [ForeignKey("user_role_id")]
        public virtual Role Role { get; set; }

        [ForeignKey("personnel_id")]
        public virtual Personnel Personnel { get; set; }

        [ForeignKey("department_id")]
        public virtual Department Department { get; set; }
    }

    // Add this to your EntityModels.cs file

    // Task entity
    [Table("tasks", Schema = "Management")]
    public class Task
    {
        [Key]
        public int task_id { get; set; }

        public int department_id { get; set; }

        [Required]
        [StringLength(255)]
        public string title { get; set; }

        [StringLength(1000)]
        public string description { get; set; }

        public int? assigned_to_user_id { get; set; }

        public DateTime created_at { get; set; }

        public DateTime? due_date { get; set; }

        [StringLength(50)]
        public string status { get; set; }

        public bool is_deleted { get; set; }

        // Navigation properties
        [ForeignKey("department_id")]
        public virtual Department Department { get; set; }
    }

}
