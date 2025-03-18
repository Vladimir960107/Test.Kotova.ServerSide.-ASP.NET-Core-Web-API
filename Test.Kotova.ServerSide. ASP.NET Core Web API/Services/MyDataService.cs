using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    /// <summary>
    /// Service for data operations specific to this application
    /// </summary>
    public class MyDataService
    {
        private readonly ILynksDbService _dbService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MyDataService> _logger;

        public MyDataService(
            ILynksDbService dbService,
            IConfiguration configuration,
            ILogger<MyDataService> logger = null)
        {
            _dbService = dbService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Gets all employees for a specific department with additional information
        /// </summary>
        /// <param name="departmentId">The ID of the department</param>
        /// <returns>List of employees with additional information</returns>
        public async Task<List<EmployeeDetails>> GetEmployeeDetailsAsync(int departmentId)
        {
            try
            {
                var employees = await _dbService.GetEmployeesByDepartmentAsync(departmentId);
                var employeeDetails = new List<EmployeeDetails>();

                foreach (var employee in employees)
                {
                    var details = new EmployeeDetails
                    {
                        EmployeeId = employee.personnel_id,
                        FullName = employee.full_name,
                        JobPosition = employee.job_position,
                        Group = employee.group,
                        BirthDate = employee.birth_date,
                        Gender = employee.gender,
                        IsDriver = employee.is_driver,
                        IsWorking = employee.is_working_in_department,
                        PersonnelNumber = employee.Personnel?.personnel_number,
                        InstructionsPassed = await GetPassedInstructionsCountAsync(employee.personnel_id, departmentId)
                    };

                    employeeDetails.Add(details);
                }

                return employeeDetails;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error getting employee details for department {departmentId}");
                throw;
            }
        }

        /// <summary>
        /// Gets the count of passed instructions for a specific employee
        /// </summary>
        /// <param name="personnelId">The ID of the personnel</param>
        /// <param name="departmentId">The ID of the department</param>
        /// <returns>Count of passed instructions</returns>
        private async Task<int> GetPassedInstructionsCountAsync(int personnelId, int departmentId)
        {
            try
            {
                // This would typically query the database to count passed instructions
                // For now, we'll just return 0 as a placeholder
                return 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error getting passed instructions count for personnel {personnelId}");
                return 0;
            }
        }

        /// <summary>
        /// Gets all instructions with their status for a specific employee
        /// </summary>
        /// <param name="personnelId">The ID of the personnel</param>
        /// <param name="departmentId">The ID of the department</param>
        /// <returns>List of instructions with status</returns>
        public async Task<List<InstructionWithStatus>> GetInstructionsWithStatusAsync(int personnelId, int departmentId)
        {
            try
            {
                var instructions = await _dbService.GetInstructionsByDepartmentAsync(departmentId);
                var instructionsWithStatus = new List<InstructionWithStatus>();

                foreach (var instruction in instructions)
                {
                    var status = new InstructionWithStatus
                    {
                        InstructionId = instruction.instruction_id,
                        Cause = instruction.cause_of_instruction,
                        BeginDate = instruction.begin_date,
                        EndDate = instruction.end_date,
                        TypeName = instruction.InstructionType?.name_of_type_instruction,
                        IsPassed = false, // Default to false, we would update this based on actual data
                        PassedDate = null,
                        PersonnelNumber = instruction.cause_of_instruction.ExtractTenDigitNumber()
                    };

                    instructionsWithStatus.Add(status);
                }

                return instructionsWithStatus;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error getting instructions with status for personnel {personnelId} in department {departmentId}");
                throw;
            }
        }
    }

    /// <summary>
    /// Details about an employee
    /// </summary>
    public class EmployeeDetails
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string JobPosition { get; set; }
        public string Group { get; set; }
        public DateTime BirthDate { get; set; }
        public byte Gender { get; set; }
        public bool IsDriver { get; set; }
        public bool IsWorking { get; set; }
        public string PersonnelNumber { get; set; }
        public int InstructionsPassed { get; set; }
    }

    /// <summary>
    /// Instruction with its status for a specific employee
    /// </summary>
    public class InstructionWithStatus
    {
        public int InstructionId { get; set; }
        public string Cause { get; set; }
        public DateTime BeginDate { get; set; }
        public DateTime EndDate { get; set; }
        public string TypeName { get; set; }
        public bool IsPassed { get; set; }
        public DateTime? PassedDate { get; set; }
        public string PersonnelNumber { get; set; }
    }

    /// <summary>
    /// Extension methods for instructions
    /// </summary>
    public static class InstructionExtensions
    {
        /// <summary>
        /// Extracts a ten-digit number from an instruction cause
        /// </summary>
        /// <param name="causeOfInstruction">The instruction cause text</param>
        /// <returns>Ten-digit number if found, null otherwise</returns>
        public static string ExtractTenDigitNumber(this string causeOfInstruction)
        {
            if (string.IsNullOrEmpty(causeOfInstruction))
                return null;

            var match = Regex.Match(causeOfInstruction, @"Вводный инструктаж для (\d{10})");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}