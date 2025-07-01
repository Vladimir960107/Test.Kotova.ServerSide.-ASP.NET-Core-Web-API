using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Kotova.CommonClasses;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseComparisonController : ControllerBase
    {
        private readonly LynksDbContext _lynksDbContext;
        private readonly TransElectroDbContext _transElectroDbContext;
        private readonly ILogger<DatabaseComparisonController> _logger;

        public DatabaseComparisonController(
            LynksDbContext lynksDbContext,
            TransElectroDbContext transElectroDbContext,
            ILogger<DatabaseComparisonController> logger)
        {
            _lynksDbContext = lynksDbContext;
            _transElectroDbContext = transElectroDbContext;
            _logger = logger;
        }

        /// <summary>
        /// Gets all employees from LynksDatabase with differences highlighted
        /// </summary>
        /// <remarks>
        /// This endpoint returns all employees from the Lynks database and marks those
        /// that have differences with the TransElectro database. Red rows should be
        /// displayed for employees with differences.
        /// </remarks>
        /// <returns>List of employees with difference indicators</returns>
        /// <response code="200">Successfully retrieved employee comparison data</response>
        /// <response code="401">Unauthorized - user not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("employees-with-differences")]
        [Authorize(Roles = "Administrator, Coordinator")]
        public async Task<ActionResult<IEnumerable<EmployeeComparisonDto>>> GetEmployeesWithDifferences()
        {
            try
            {
                _logger.LogInformation("Starting employee comparison between databases");

                // Get all employees from Lynks database
                var lynksEmployees = await _lynksDbContext.EmployeesByDepartment
                    .Include(e => e.Personnel)
                    .Include(e => e.Department)
                    .Where(e => e.is_working_in_department)
                    .ToListAsync();

                _logger.LogInformation($"Found {lynksEmployees.Count} employees in Lynks database");

                // Get all employees from TransElectro database
                var transElectroEmployees = await _transElectroDbContext.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Position)
                    .Where(e => !e.IsHidden.HasValue || !e.IsHidden.Value)
                    .ToListAsync();

                _logger.LogInformation($"Found {transElectroEmployees.Count} employees in TransElectro database");

                var comparisonResults = new List<EmployeeComparisonDto>();

                foreach (var lynksEmployee in lynksEmployees)
                {
                    var personnelNumber = lynksEmployee.Personnel?.personnel_number;
                    if (string.IsNullOrEmpty(personnelNumber))
                        continue;

                    // Find corresponding employee in TransElectro database by personnel number
                    var transElectroEmployee = transElectroEmployees
                        .FirstOrDefault(te => te.PersonnelNumber == personnelNumber);

                    var comparisonDto = new EmployeeComparisonDto
                    {
                        PersonnelNumber = personnelNumber,
                        FullName = lynksEmployee.full_name,
                        DepartmentName = lynksEmployee.Department?.department_name ?? "Unknown",
                        PositionName = lynksEmployee.job_position,
                        Email = "", // Lynks doesn't have email field in EmployeeByDepartment
                        HasDifferences = false,
                        DifferenceFields = new List<string>(),
                        LynksData = new TelpEmployeeDto
                        {
                            FullName = lynksEmployee.full_name,
                            DepartmentName = lynksEmployee.Department?.department_name ?? "Unknown",
                            PositionName = lynksEmployee.job_position,
                            Email = "", // Will be populated from Users table if needed
                            PersonnelNumber = personnelNumber
                        }
                    };

                    if (transElectroEmployee != null)
                    {
                        // Compare the data and identify differences
                        var differences = CompareEmployeeData(lynksEmployee, transElectroEmployee);
                        comparisonDto.HasDifferences = differences.Any();
                        comparisonDto.DifferenceFields = differences;

                        comparisonDto.TransElectroData = new TransElectroEmployeeDto
                        {
                            FullName = transElectroEmployee.FullName,
                            DepartmentName = transElectroEmployee.Department?.Name ?? "Unknown",
                            PositionName = transElectroEmployee.Position?.Name ?? "Unknown",
                            Email = transElectroEmployee.Email ?? "",
                            PersonnelNumber = transElectroEmployee.PersonnelNumber ?? ""
                        };
                    }
                    else
                    {
                        // Employee exists in Lynks but not in TransElectro
                        comparisonDto.HasDifferences = true;
                        comparisonDto.DifferenceFields.Add("Employee not found in TransElectro database");
                        comparisonDto.TransElectroData = null;
                    }

                    comparisonResults.Add(comparisonDto);
                }

                var employeesWithDifferences = comparisonResults.Count(e => e.HasDifferences);
                _logger.LogInformation($"Found {employeesWithDifferences} employees with differences");

                return Ok(comparisonResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing employee databases");
                return StatusCode(500, "Internal server error while comparing databases");
            }
        }

        /// <summary>
        /// Gets detailed comparison data for a specific employee
        /// </summary>
        /// <remarks>
        /// This endpoint returns detailed comparison data for a specific employee
        /// identified by personnel number. Used when double-clicking on a row
        /// to show the detailed comparison form.
        /// </remarks>
        /// <param name="personnelNumber">The personnel number of the employee</param>
        /// <returns>Detailed comparison data for the employee</returns>
        /// <response code="200">Successfully retrieved detailed comparison data</response>
        /// <response code="404">Employee not found</response>
        /// <response code="401">Unauthorized - user not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("employee-details/{personnelNumber}")]
        [Authorize(Roles = "Administrator, Coordinator")]
        public async Task<ActionResult<EmployeeComparisonDto>> GetEmployeeComparisonDetails(string personnelNumber)
        {
            try
            {
                _logger.LogInformation($"Getting detailed comparison for employee: {personnelNumber}");

                // Get employee from Lynks database
                var lynksEmployee = await _lynksDbContext.EmployeesByDepartment
                    .Include(e => e.Personnel)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Personnel.personnel_number == personnelNumber);

                if (lynksEmployee == null)
                {
                    return NotFound($"Employee with personnel number {personnelNumber} not found in Lynks database");
                }

                // Get corresponding employee from TransElectro database
                var transElectroEmployee = await _transElectroDbContext.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Position)
                    .FirstOrDefaultAsync(e => e.PersonnelNumber == personnelNumber);

                // Get user data from Lynks database for email
                var lynksUser = await _lynksDbContext.Users
                    .FirstOrDefaultAsync(u => u.personnel_id == lynksEmployee.personnel_id);

                var detailedComparison = new EmployeeComparisonDto
                {
                    PersonnelNumber = personnelNumber,
                    FullName = lynksEmployee.full_name,
                    DepartmentName = lynksEmployee.Department?.department_name ?? "Unknown",
                    PositionName = lynksEmployee.job_position,
                    Email = lynksUser?.current_email ?? "",
                    LynksData = new TelpEmployeeDto
                    {
                        FullName = lynksEmployee.full_name,
                        DepartmentName = lynksEmployee.Department?.department_name ?? "Unknown",
                        PositionName = lynksEmployee.job_position,
                        Email = lynksUser?.current_email ?? "",
                        PersonnelNumber = personnelNumber
                    }
                };

                if (transElectroEmployee != null)
                {
                    var differences = CompareEmployeeData(lynksEmployee, transElectroEmployee);
                    detailedComparison.HasDifferences = differences.Any();
                    detailedComparison.DifferenceFields = differences;

                    detailedComparison.TransElectroData = new TransElectroEmployeeDto
                    {
                        FullName = transElectroEmployee.FullName,
                        DepartmentName = transElectroEmployee.Department?.Name ?? "Unknown",
                        PositionName = transElectroEmployee.Position?.Name ?? "Unknown",
                        Email = transElectroEmployee.Email ?? "",
                        PersonnelNumber = transElectroEmployee.PersonnelNumber ?? ""
                    };
                }
                else
                {
                    detailedComparison.HasDifferences = true;
                    detailedComparison.DifferenceFields = new List<string> { "Employee not found in TransElectro database" };
                    detailedComparison.TransElectroData = null;
                }

                return Ok(detailedComparison);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting detailed comparison for employee {personnelNumber}");
                return StatusCode(500, "Internal server error while getting employee details");
            }
        }

        /// <summary>
        /// Gets comparison statistics between the two databases
        /// </summary>
        /// <remarks>
        /// This endpoint returns statistical information about differences
        /// between the two databases, useful for dashboard displays.
        /// </remarks>
        /// <returns>Database comparison statistics</returns>
        /// <response code="200">Successfully retrieved comparison statistics</response>
        /// <response code="401">Unauthorized - user not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("comparison-statistics")]
        [Authorize(Roles = "Administrator, Coordinator")]
        public async Task<ActionResult<object>> GetComparisonStatistics()
        {
            try
            {
                _logger.LogInformation("Getting database comparison statistics");

                var lynksEmployeeCount = await _lynksDbContext.EmployeesByDepartment
                    .Where(e => e.is_working_in_department)
                    .CountAsync();

                var transElectroEmployeeCount = await _transElectroDbContext.Employees
                    .Where(e => !e.IsHidden.HasValue || !e.IsHidden.Value)
                    .CountAsync();

                // Get employees with personnel numbers from both databases
                var lynksPersonnelNumbers = await _lynksDbContext.EmployeesByDepartment
                    .Include(e => e.Personnel)
                    .Where(e => e.is_working_in_department && e.Personnel.personnel_number != null)
                    .Select(e => e.Personnel.personnel_number)
                    .ToListAsync();

                var transElectroPersonnelNumbers = await _transElectroDbContext.Employees
                    .Where(e => (!e.IsHidden.HasValue || !e.IsHidden.Value) &&
                               e.PersonnelNumber != null)
                    .Select(e => e.PersonnelNumber)
                    .ToListAsync();

                var commonPersonnelNumbers = lynksPersonnelNumbers.Intersect(transElectroPersonnelNumbers).Count();
                var onlyInLynks = lynksPersonnelNumbers.Except(transElectroPersonnelNumbers).Count();
                var onlyInTransElectro = transElectroPersonnelNumbers.Except(lynksPersonnelNumbers).Count();

                var statistics = new
                {
                    LynksEmployeeCount = lynksEmployeeCount,
                    TransElectroEmployeeCount = transElectroEmployeeCount,
                    CommonEmployees = commonPersonnelNumbers,
                    OnlyInLynks = onlyInLynks,
                    OnlyInTransElectro = onlyInTransElectro,
                    ComparisonDate = DateTime.UtcNow
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comparison statistics");
                return StatusCode(500, "Internal server error while getting statistics");
            }
        }

        /// <summary>
        /// Synchronizes data from TransElectro to Lynks database (for future implementation)
        /// </summary>
        /// <remarks>
        /// This endpoint will be used to resolve differences by updating Lynks database
        /// with data from TransElectro database. This is a placeholder for future implementation.
        /// </remarks>
        /// <param name="personnelNumber">Personnel number of employee to synchronize</param>
        /// <param name="syncRequest">Synchronization request details</param>
        /// <returns>Result of synchronization operation</returns>
        /// <response code="200">Successfully synchronized employee data</response>
        /// <response code="404">Employee not found</response>
        /// <response code="401">Unauthorized - user not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("synchronize-employee/{personnelNumber}")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<object>> SynchronizeEmployee(
            string personnelNumber,
            [FromBody] EmployeeSyncRequest syncRequest)
        {
            try
            {
                _logger.LogInformation($"Synchronizing employee: {personnelNumber}");

                // TODO: Implement synchronization logic
                // This is a placeholder for future implementation

                return Ok(new
                {
                    Message = $"Synchronization endpoint ready for employee {personnelNumber}",
                    Status = "Not yet implemented",
                    PersonnelNumber = personnelNumber,
                    RequestedFields = syncRequest?.FieldsToSync ?? new List<string>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error synchronizing employee {personnelNumber}");
                return StatusCode(500, "Internal server error while synchronizing employee");
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Compares employee data between Lynks and TransElectro databases
        /// </summary>
        /// <param name="lynksEmployee">Employee from Lynks database</param>
        /// <param name="transElectroEmployee">Employee from TransElectro database</param>
        /// <returns>List of field names that have differences</returns>
        private List<string> CompareEmployeeData(EmployeeByDepartment lynksEmployee, TelpEmployee transElectroEmployee)
        {
            var differences = new List<string>();

            // Compare full name
            if (!string.Equals(lynksEmployee.full_name?.Trim(), transElectroEmployee.FullName?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                differences.Add("FullName");
            }

            // Compare department name
            var lynksDepartmentName = lynksEmployee.Department?.department_name?.Trim();
            var transElectroDepartmentName = transElectroEmployee.Department?.Name?.Trim();
            if (!string.Equals(lynksDepartmentName, transElectroDepartmentName, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add("DepartmentName");
            }

            // Compare position/job title
            var lynksPosition = lynksEmployee.job_position?.Trim();
            var transElectroPosition = transElectroEmployee.Position?.Name?.Trim();
            if (!string.Equals(lynksPosition, transElectroPosition, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add("Position");
            }

            // Note: Email comparison would require joining with Users table for Lynks data
            // This can be implemented in the detailed comparison method

            return differences;
        }

        #endregion
    }

    /// <summary>
    /// Request model for employee synchronization
    /// </summary>
    public class EmployeeSyncRequest
    {
        /// <summary>
        /// List of fields to synchronize
        /// </summary>
        public List<string> FieldsToSync { get; set; } = new List<string>();

        /// <summary>
        /// Direction of synchronization (ToLynks, ToTransElectro)
        /// </summary>
        public string SyncDirection { get; set; } = "ToLynks";

        /// <summary>
        /// Whether to overwrite existing data
        /// </summary>
        public bool OverwriteExisting { get; set; } = false;
    }
}