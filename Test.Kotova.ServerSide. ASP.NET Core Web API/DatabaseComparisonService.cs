using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Task = System.Threading.Tasks.Task;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    /// <summary>
    /// Service for comparing data between LynksDataBase and ТрансэлектропроектDataBase
    /// </summary>
    public interface IDatabaseComparisonService
    {
        Task<List<EmployeeDifferenceDto>> GetEmployeeDifferencesAsync();
        Task<EmployeeComparisonDetailDto> GetEmployeeComparisonDetailAsync(string personnelNumber);
        Task<List<DepartmentDifferenceDto>> GetDepartmentDifferencesAsync();
        Task<DatabaseSyncStatusDto> GetSyncStatusAsync();
        Task<bool> SyncEmployeeAsync(string personnelNumber, EmployeeSyncDirection direction);
    }

    public class DatabaseComparisonService : IDatabaseComparisonService
    {
        private readonly LynksDbContext _lynksContext;
        private readonly TransElectroDbContext _telpContext;
        private readonly ILogger<DatabaseComparisonService> _logger;

        public DatabaseComparisonService(
            LynksDbContext lynksContext,
            TransElectroDbContext telpContext,
            ILogger<DatabaseComparisonService> logger)
        {
            _lynksContext = lynksContext;
            _telpContext = telpContext;
            _logger = logger;
        }

        /// <summary>
        /// Gets all employees with differences between the two databases
        /// </summary>
        public async Task<List<EmployeeDifferenceDto>> GetEmployeeDifferencesAsync()
        {
            try
            {
                _logger.LogInformation("Starting employee differences comparison");

                // Get all employees from Lynks database
                var lynksEmployees = await _lynksContext.EmployeesByDepartment
                    .Include(e => e.Personnel)
                    .Include(e => e.Department)
                    .Where(e => e.Personnel != null)
                    .ToListAsync();

                // Get all employees from TELP database
                var telpEmployees = await _telpContext.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Position)
                    .Where(e => e.IsHidden != true) // Only get non-hidden employees
                    .ToListAsync();

                var differences = new List<EmployeeDifferenceDto>();

                // Check each Lynks employee against TELP
                foreach (var lynksEmployee in lynksEmployees)
                {
                    var personnelNumber = lynksEmployee.Personnel?.personnel_number;
                    if (string.IsNullOrEmpty(personnelNumber)) continue;

                    // Find corresponding TELP employee
                    var telpEmployee = telpEmployees.FirstOrDefault(t =>
                        t.PersonnelNumber == personnelNumber ||
                        (CompareNames(t.FullName, lynksEmployee.full_name) &&
                         CompareDates(t.BirthDate, lynksEmployee.birth_date)));

                    var differenceType = DetermineDifferenceType(lynksEmployee, telpEmployee);
                    if (differenceType != EmployeeDifferenceType.None)
                    {
                        differences.Add(new EmployeeDifferenceDto
                        {
                            PersonnelNumber = personnelNumber,
                            FullName = lynksEmployee.full_name,
                            BirthDate = lynksEmployee.birth_date,
                            Department = lynksEmployee.Department?.department_name ?? "Unknown",
                            Position = lynksEmployee.job_position,
                            DifferenceType = differenceType,
                            DifferenceDescription = GetDifferenceDescription(lynksEmployee, telpEmployee),
                            LastUpdated = DateTime.UtcNow,
                            IsInLynks = true,
                            IsInTelp = telpEmployee != null
                        });
                    }
                }

                // Check for TELP employees not in Lynks
                foreach (var telpEmployee in telpEmployees)
                {
                    if (string.IsNullOrEmpty(telpEmployee.PersonnelNumber)) continue;

                    var lynksEmployee = lynksEmployees.FirstOrDefault(l =>
                        l.Personnel?.personnel_number == telpEmployee.PersonnelNumber ||
                        (CompareNames(l.full_name, telpEmployee.FullName)));

                    if (lynksEmployee == null)
                    {
                        differences.Add(new EmployeeDifferenceDto
                        {
                            PersonnelNumber = telpEmployee.PersonnelNumber,
                            FullName = telpEmployee.FullName,
                            BirthDate = telpEmployee.BirthDate, // Note: Need to check if TELP has birth date
                            Department = telpEmployee.Department?.Name ?? "Unknown",
                            Position = telpEmployee.Position?.Name ?? "Unknown",
                            DifferenceType = EmployeeDifferenceType.OnlyInTelp,
                            DifferenceDescription = "Employee exists only in TELP database",
                            LastUpdated = DateTime.UtcNow,
                            IsInLynks = false,
                            IsInTelp = true
                        });
                    }
                }

                _logger.LogInformation($"Found {differences.Count} employee differences");
                return differences.OrderBy(d => d.FullName).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing employee data between databases");
                throw;
            }
        }

        /// <summary>
        /// Gets detailed comparison data for a specific employee
        /// </summary>
        public async Task<EmployeeComparisonDetailDto> GetEmployeeComparisonDetailAsync(string personnelNumber)
        {
            try
            {
                // Get employee from Lynks
                var lynksEmployee = await _lynksContext.EmployeesByDepartment
                    .Include(e => e.Personnel)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Personnel != null &&
                                           e.Personnel.personnel_number == personnelNumber);

                // Get employee from TELP
                var telpEmployee = await _telpContext.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Position)
                    .FirstOrDefaultAsync(e => e.PersonnelNumber == personnelNumber);

                var detail = new EmployeeComparisonDetailDto
                {
                    PersonnelNumber = personnelNumber,
                    LynksData = lynksEmployee != null ? new EmployeeDataDto
                    {
                        FullName = lynksEmployee.full_name,
                        BirthDate = lynksEmployee.birth_date,
                        Department = lynksEmployee.Department?.department_name,
                        Position = lynksEmployee.job_position,
                        Gender = lynksEmployee.gender.ToString(),
                        IsDriver = lynksEmployee.is_driver,
                        WorkGroup = lynksEmployee.group,
                        IsWorking = lynksEmployee.is_working_in_department,
                        LastUpdated = DateTime.UtcNow // We don't have this field in Lynks, so use current time
                    } : null,
                    TelpData = telpEmployee != null ? new EmployeeDataDto
                    {
                        FullName = telpEmployee.FullName,
                        BirthDate = DateTime.MinValue, // TELP doesn't store birth date
                        Department = telpEmployee.Department?.Name,
                        Position = telpEmployee.Position?.Name,
                        Gender = null, // TELP doesn't store gender
                        IsDriver = false, // TELP doesn't store driver status
                        WorkGroup = telpEmployee.GroupId?.ToString(),
                        IsWorking = telpEmployee.IsHidden != true,
                        Email = telpEmployee.Email,
                        PhoneNumber = null, // TELP doesn't store phone
                        WorkplaceNumber = telpEmployee.Room ?? telpEmployee.Room2,
                        HireDate = null, // TELP doesn't store hire date
                        LastUpdated = DateTime.UtcNow // TELP doesn't track update time
                    } : null,
                    FieldDifferences = CompareEmployeeFields(lynksEmployee, telpEmployee)
                };

                return detail;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting employee comparison details for personnel number {personnelNumber}");
                throw;
            }
        }

        /// <summary>
        /// Gets department differences between databases
        /// </summary>
        public async Task<List<DepartmentDifferenceDto>> GetDepartmentDifferencesAsync()
        {
            try
            {
                var lynksDepartments = await _lynksContext.Departments.ToListAsync();
                var telpDepartments = await _telpContext.Departments.Where(d => d.IsHidden != true).ToListAsync();

                var differences = new List<DepartmentDifferenceDto>();

                // Compare departments by name
                foreach (var lynksDept in lynksDepartments)
                {
                    var telpDept = telpDepartments.FirstOrDefault(t =>
                        string.Equals(t.Name?.Trim(), lynksDept.department_name.Trim(),
                                    StringComparison.OrdinalIgnoreCase));

                    if (telpDept == null)
                    {
                        differences.Add(new DepartmentDifferenceDto
                        {
                            DepartmentName = lynksDept.department_name,
                            DifferenceType = DepartmentDifferenceType.OnlyInLynks,
                            LynksId = lynksDept.department_id,
                            TelpId = null
                        });
                    }
                }

                foreach (var telpDept in telpDepartments)
                {
                    var lynksDept = lynksDepartments.FirstOrDefault(l =>
                        string.Equals(l.department_name.Trim(), telpDept.Name?.Trim(),
                                    StringComparison.OrdinalIgnoreCase));

                    if (lynksDept == null)
                    {
                        differences.Add(new DepartmentDifferenceDto
                        {
                            DepartmentName = telpDept.Name ?? "Unknown",
                            DifferenceType = DepartmentDifferenceType.OnlyInTelp,
                            LynksId = null,
                            TelpId = telpDept.Code
                        });
                    }
                }

                return differences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing department data between databases");
                throw;
            }
        }

        /// <summary>
        /// Gets overall synchronization status
        /// </summary>
        public async Task<DatabaseSyncStatusDto> GetSyncStatusAsync()
        {
            try
            {
                var lynksEmployeeCount = await _lynksContext.EmployeesByDepartment
                    .CountAsync(e => e.Personnel != null);
                var telpEmployeeCount = await _telpContext.Employees
                    .CountAsync(e => e.IsHidden != true);

                var differences = await GetEmployeeDifferencesAsync();
                var departmentDifferences = await GetDepartmentDifferencesAsync();

                return new DatabaseSyncStatusDto
                {
                    LynksEmployeeCount = lynksEmployeeCount,
                    TelpEmployeeCount = telpEmployeeCount,
                    TotalDifferences = differences.Count,
                    OnlyInLynks = differences.Count(d => d.DifferenceType == EmployeeDifferenceType.OnlyInLynks),
                    OnlyInTelp = differences.Count(d => d.DifferenceType == EmployeeDifferenceType.OnlyInTelp),
                    DataMismatch = differences.Count(d => d.DifferenceType == EmployeeDifferenceType.DataMismatch),
                    DepartmentDifferences = departmentDifferences.Count,
                    LastChecked = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sync status");
                throw;
            }
        }

        /// <summary>
        /// Synchronizes a specific employee between databases
        /// </summary>
        public async Task<bool> SyncEmployeeAsync(string personnelNumber, EmployeeSyncDirection direction)
        {
            try
            {
                _logger.LogInformation($"Starting sync for employee {personnelNumber}, direction: {direction}");

                // Implementation would depend on business requirements
                // For now, just log the sync attempt
                await LogSyncAttempt(personnelNumber, direction.ToString(), "Sync functionality not yet implemented");

                // TODO: Implement actual sync logic based on direction
                return false; // Return false until implemented
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error syncing employee {personnelNumber}");
                await LogSyncAttempt(personnelNumber, direction.ToString(), ex.Message);
                return false;
            }
        }

        #region Private Helper Methods

        private EmployeeDifferenceType DetermineDifferenceType(EmployeeByDepartment lynksEmployee, TelpEmployee? telpEmployee)
        {
            if (telpEmployee == null)
                return EmployeeDifferenceType.OnlyInLynks;

            // Check for data mismatches
            if (!CompareNames(lynksEmployee.full_name, telpEmployee.FullName) ||
                !ComparePositions(lynksEmployee.job_position, telpEmployee.Position?.Name))
            {
                return EmployeeDifferenceType.DataMismatch;
            }

            return EmployeeDifferenceType.None;
        }

        private string GetDifferenceDescription(EmployeeByDepartment lynksEmployee, TelpEmployee? telpEmployee)
        {
            if (telpEmployee == null)
                return "Employee exists only in Lynks database";

            var differences = new List<string>();

            if (!CompareNames(lynksEmployee.full_name, telpEmployee.FullName))
                differences.Add($"Name: Lynks='{lynksEmployee.full_name}' vs TELP='{telpEmployee.FullName}'");

            if (!ComparePositions(lynksEmployee.job_position, telpEmployee.Position?.Name))
                differences.Add($"Position: Lynks='{lynksEmployee.job_position}' vs TELP='{telpEmployee.Position?.Name}'");

            if (!CompareDepartments(lynksEmployee.Department?.department_name, telpEmployee.Department?.Name))
                differences.Add($"Department: Lynks='{lynksEmployee.Department?.department_name}' vs TELP='{telpEmployee.Department?.Name}'");

            return string.Join("; ", differences);
        }

        private List<FieldDifferenceDto> CompareEmployeeFields(EmployeeByDepartment? lynksEmployee, TelpEmployee? telpEmployee)
        {
            var differences = new List<FieldDifferenceDto>();

            if (lynksEmployee == null || telpEmployee == null)
                return differences;

            // Compare each field
            CompareField(differences, "FullName", lynksEmployee.full_name, telpEmployee.FullName);
            CompareField(differences, "Position", lynksEmployee.job_position, telpEmployee.Position?.Name);
            CompareField(differences, "Department", lynksEmployee.Department?.department_name, telpEmployee.Department?.Name);
            CompareField(differences, "Email", null, telpEmployee.Email); // Lynks doesn't have email
            CompareField(differences, "WorkGroup", lynksEmployee.group, telpEmployee.GroupId?.ToString());
            CompareField(differences, "Room", null, telpEmployee.Room ?? telpEmployee.Room2); // Lynks doesn't have room info

            return differences;
        }

        private void CompareField(List<FieldDifferenceDto> differences, string fieldName, string? lynksValue, string? telpValue)
        {
            lynksValue = lynksValue?.Trim();
            telpValue = telpValue?.Trim();

            if (!string.Equals(lynksValue, telpValue, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add(new FieldDifferenceDto
                {
                    FieldName = fieldName,
                    LynksValue = lynksValue,
                    TelpValue = telpValue
                });
            }
        }

        private bool CompareNames(string? name1, string? name2)
        {
            if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
                return false;

            return string.Equals(name1.Trim(), name2.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private bool CompareDates(DateTime date1, DateTime date2)
        {
            return date1.Date == date2.Date;
        }

        private bool ComparePositions(string? position1, string? position2)
        {
            if (string.IsNullOrWhiteSpace(position1) || string.IsNullOrWhiteSpace(position2))
                return string.IsNullOrWhiteSpace(position1) && string.IsNullOrWhiteSpace(position2);

            return string.Equals(position1.Trim(), position2.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private bool CompareDepartments(string? dept1, string? dept2)
        {
            if (string.IsNullOrWhiteSpace(dept1) || string.IsNullOrWhiteSpace(dept2))
                return string.IsNullOrWhiteSpace(dept1) && string.IsNullOrWhiteSpace(dept2);

            return string.Equals(dept1.Trim(), dept2.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private async Task LogSyncAttempt(string personnelNumber, string operation, string message)
        {
            try
            {
                var syncLog = new TelpDataSyncLog
                {
                    SyncType = "Employee",
                    EntityId = personnelNumber,
                    Operation = operation,
                    SyncDate = DateTime.UtcNow,
                    IsSuccessful = false,
                    ErrorMessage = message
                };

                _telpContext.DataSyncLogs.Add(syncLog);
                await _telpContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to log sync attempt for {personnelNumber}");
            }
        }

        #endregion
    }

    #region DTOs

    /// <summary>
    /// DTO for employee differences
    /// </summary>
    public class EmployeeDifferenceDto
    {
        public string PersonnelNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public EmployeeDifferenceType DifferenceType { get; set; }
        public string DifferenceDescription { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public bool IsInLynks { get; set; }
        public bool IsInTelp { get; set; }
    }

    /// <summary>
    /// DTO for detailed employee comparison
    /// </summary>
    public class EmployeeComparisonDetailDto
    {
        public string PersonnelNumber { get; set; } = string.Empty;
        public EmployeeDataDto? LynksData { get; set; }
        public EmployeeDataDto? TelpData { get; set; }
        public List<FieldDifferenceDto> FieldDifferences { get; set; } = new List<FieldDifferenceDto>();
    }

    /// <summary>
    /// DTO for employee data from either database
    /// </summary>
    public class EmployeeDataDto
    {
        public string? FullName { get; set; }
        public DateTime BirthDate { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? Gender { get; set; }
        public bool IsDriver { get; set; }
        public string? WorkGroup { get; set; }
        public bool IsWorking { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? WorkplaceNumber { get; set; }
        public DateTime? HireDate { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// DTO for field-level differences
    /// </summary>
    public class FieldDifferenceDto
    {
        public string FieldName { get; set; } = string.Empty;
        public string? LynksValue { get; set; }
        public string? TelpValue { get; set; }
    }

    /// <summary>
    /// DTO for department differences
    /// </summary>
    public class DepartmentDifferenceDto
    {
        public string DepartmentName { get; set; } = string.Empty;
        public DepartmentDifferenceType DifferenceType { get; set; }
        public int? LynksId { get; set; }
        public int? TelpId { get; set; }
    }

    /// <summary>
    /// DTO for database sync status
    /// </summary>
    public class DatabaseSyncStatusDto
    {
        public int LynksEmployeeCount { get; set; }
        public int TelpEmployeeCount { get; set; }
        public int TotalDifferences { get; set; }
        public int OnlyInLynks { get; set; }
        public int OnlyInTelp { get; set; }
        public int DataMismatch { get; set; }
        public int DepartmentDifferences { get; set; }
        public DateTime LastChecked { get; set; }
    }

    #endregion

    #region Enums

    /// <summary>
    /// Types of employee differences
    /// </summary>
    public enum EmployeeDifferenceType
    {
        None = 0,
        OnlyInLynks = 1,
        OnlyInTelp = 2,
        DataMismatch = 3
    }

    /// <summary>
    /// Types of department differences
    /// </summary>
    public enum DepartmentDifferenceType
    {
        OnlyInLynks = 1,
        OnlyInTelp = 2,
        NameMismatch = 3
    }

    /// <summary>
    /// Direction for employee synchronization
    /// </summary>
    public enum EmployeeSyncDirection
    {
        LynksToTelp = 1,
        TelpToLynks = 2
    }

    #endregion

    #region Missing Entity Definitions (You'll need to implement these)

    // TODO: Implement TransElectroDbContext based on your database schema
    public class TransElectroDbContext : DbContext
    {
        public DbSet<TelpEmployee> Employees { get; set; }
        public DbSet<TelpDepartment> Departments { get; set; }
        public DbSet<TelpPosition> Positions { get; set; }
        public DbSet<TelpDataSyncLog> DataSyncLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Configure connection to ТрансэлектропроектDB
            optionsBuilder.UseSqlServer("YourConnectionStringHere");
        }
    }

    // TODO: Map these to your actual TELP database tables
    public class TelpEmployee
    {
        public int Code { get; set; } // Код
        public string FullName { get; set; } = string.Empty; // ФИО
        public int? DepartmentId { get; set; } // Отдел
        public int? PositionId { get; set; } // Должность
        public string? ComputerName { get; set; } // Имя_Компьютера
        public bool? HasAccess { get; set; } // Доступ
        public string? Email { get; set; } // e-mail
        public string? UserName { get; set; } // User_Name
        public bool? IsHidden { get; set; } // Hide
        public string? Room2 { get; set; } // Комната2
        public bool? Messages { get; set; } // Сообщения
        public int? AccessRights { get; set; } // Права_доступа
        public string? Room { get; set; } // Комната
        public int? GroupId { get; set; } // Группа
        public string? PersonnelNumber { get; set; } // Табельный_номер

        // Note: TELP database doesn't have birth date - you might need to add this
        public DateTime BirthDate { get; set; } = DateTime.MinValue;

        public virtual TelpDepartment? Department { get; set; }
        public virtual TelpPosition? Position { get; set; }
    }

    public class TelpDepartment
    {
        public int Code { get; set; } // Код
        public string? Name { get; set; } // Наименование
        public string? ShortName { get; set; } // Кратко
        public int? ChiefId { get; set; } // Начальник
        public bool? IsHidden { get; set; } // Скрыть
        // Add other properties as needed
    }

    public class TelpPosition
    {
        public int Code { get; set; } // Код
        public string? Name { get; set; } // Наименование
        public string? ShortName { get; set; } // Кратко
        // Add other properties as needed
    }

    public class TelpDataSyncLog
    {
        public int Id { get; set; }
        public string SyncType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public DateTime SyncDate { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion
}