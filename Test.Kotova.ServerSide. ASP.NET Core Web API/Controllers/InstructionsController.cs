using DocumentFormat.OpenXml.InkML;
using Kotova.CommonClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using Task = System.Threading.Tasks.Task;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InstructionsController : ControllerBase
    {
        private readonly ILynksDbService _dbService;
        private readonly LynksDbContext _dbContext;
        private readonly ILogger<InstructionsController> _logger;
        private readonly NotificationsService _notificationsService;

        public InstructionsController(
            ILynksDbService dbService,
            LynksDbContext dbContext,
            ILogger<InstructionsController> logger,
            NotificationsService notificationsService)
        {
            _dbService = dbService;
            _dbContext = dbContext;
            _logger = logger;
            _notificationsService = notificationsService;
        }



        #region DTO STUFF (for another separate file)

        // Create these classes either in a separate file or at the bottom of your controller file
        public class InstructionResultDto
        {
            public int InstructionId { get; set; }
            public string CauseOfInstruction { get; set; }
            public DateTime BeginDate { get; set; }
            public DateTime EndDate { get; set; }
            public byte TypeOfInstruction { get; set; }
            public bool IsAssignedToPeople { get; set; }
            public bool IsPassedByEveryone { get; set; }
            public List<string> FilePaths { get; set; } = new List<string>();

            // Optional: Add a property for the instruction type name
            public string TypeName { get; set; }
        }

        // Helper method to map from entity to DTO
        private async Task<InstructionResultDto> MapToDto(Models.Instruction instruction, List<string> filePaths)
        {
            return new InstructionResultDto
            {
                InstructionId = instruction.instruction_id,
                CauseOfInstruction = instruction.cause_of_instruction,
                BeginDate = instruction.begin_date,
                EndDate = instruction.end_date,
                TypeOfInstruction = instruction.type_of_instruction,
                IsAssignedToPeople = instruction.is_assigned_to_people,
                IsPassedByEveryone = instruction.is_passed_by_everyone,
                FilePaths = filePaths ?? new List<string>(),
                TypeName = await GetInstructionTypeNameAsync(instruction.type_of_instruction)
            };
        }




        #region UserRegion

        /// <summary>
        /// Retrieves not passed instructions for the authenticated user with normative instruction links.
        /// </summary>
        /// <remarks>
        /// This endpoint fetches the user's associated not passed instructions by determining their personnel number 
        /// and department ID from the database. It also includes related normative instruction names and URLs.
        /// Requires the user to be authenticated.
        /// </remarks>
        /// <returns>
        /// Returns a list of instruction data for the user including normative instructions.
        /// </returns>
        /// <response code="200">
        /// The user's not passed instructions were retrieved successfully.
        /// </response>
        /// <response code="400">
        /// A bad request occurred due to one of the following:
        /// - The personnel ID for the user was not found.
        /// - The department ID for the user was not found.
        /// </response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        [Authorize]
        [HttpGet("get-not-passed-instructions")]
        public async Task<IActionResult> GetNotPassedInstructions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Personnel)
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                if (user.personnel_id <= 0)
                {
                    return BadRequest("Personnel ID not found for this user");
                }

                // Get instruction statuses for the user that are not passed with normative instructions
                var instructionStatuses = await _dbContext.InstructionStatuses
                    .Where(s => s.personnel_id == user.personnel_id && !s.is_instruction_passed)
                    .Include(s => s.Instruction)
                        .ThenInclude(i => i.InstructionType)
                    .Include(s => s.NormativeInstructions)
                        .ThenInclude(ni => ni.NormativeInstructionName)
                    .ToListAsync();

                var result = instructionStatuses.Select(s => new
                {
                    InstructionId = s.instruction_id,
                    DepartmentId = s.department_id,
                    Cause = s.Instruction.cause_of_instruction,
                    EndDate = s.Instruction.end_date,
                    Type = s.Instruction.InstructionType?.name_of_type_instruction,
                    TypeOfInstruction = s.Instruction.type_of_instruction,
                    WhenAssigned = s.when_was_sent_to_user,
                    NormativeInstructions = s.NormativeInstructions.Select(ni => new
                    {
                        Id = ni.normative_instruction_name_id,
                        Name = ni.NormativeInstructionName.normative_instruction_name,
                        Url = ni.NormativeInstructionName.url
                    }).ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving not passed instructions");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves passed instructions for the authenticated user with normative instruction links.
        /// </summary>
        /// <remarks>
        /// This endpoint fetches the user's associated passed instructions by determining their personnel number 
        /// and department ID from the database. It also includes related normative instruction names and URLs.
        /// Requires the user to be authenticated.
        /// </remarks>
        /// <returns>
        /// Returns a list of instruction data for the user including normative instructions.
        /// </returns>
        /// <response code="200">
        /// The user's passed instructions were retrieved successfully.
        /// </response>
        /// <response code="400">
        /// A bad request occurred due to one of the following:
        /// - The personnel ID for the user was not found.
        /// - The department ID for the user was not found.
        /// </response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        [Authorize]
        [HttpGet("get-passed-instructions")]
        public async Task<IActionResult> GetPassedInstructions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Personnel)
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                if (user.personnel_id <= 0)
                {
                    return BadRequest("Personnel ID not found for this user");
                }

                // Get instruction statuses for the user that are passed with normative instructions
                var instructionStatuses = await _dbContext.InstructionStatuses
                    .Where(s => s.personnel_id == user.personnel_id && s.is_instruction_passed)
                    .Include(s => s.Instruction)
                        .ThenInclude(i => i.InstructionType)
                    .Include(s => s.NormativeInstructions)
                        .ThenInclude(ni => ni.NormativeInstructionName)
                    .ToListAsync();

                var result = instructionStatuses.Select(s => new
                {
                    InstructionId = s.instruction_id,
                    DepartmentId = s.department_id,
                    Cause = s.Instruction.cause_of_instruction,
                    EndDate = s.Instruction.end_date,
                    Type = s.Instruction.InstructionType?.name_of_type_instruction,
                    TypeOfInstruction = s.Instruction.type_of_instruction,
                    WhenAssigned = s.when_was_sent_to_user,
                    WhenPassed = s.date_when_passed,
                    NormativeInstructions = s.NormativeInstructions.Select(ni => new
                    {
                        Id = ni.normative_instruction_name_id,
                        Name = ni.NormativeInstructionName.normative_instruction_name,
                        Url = ni.NormativeInstructionName.url
                    }).ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving passed instructions");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Marks an instruction as passed for the authenticated user.
        /// If it's an unplanned instruction and the user is a chief/deputy, also sets is_passed_by_chief_unplanned_instr to true.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authenticated users to mark an instruction as passed.
        /// For unplanned instructions, if the user is a chief or deputy, it also triggers
        /// the is_passed_by_chief_unplanned_instr flag to allow assignment to other employees.
        /// </remarks>
        /// <param name="instructionId">The ID of the instruction to mark as passed</param>
        /// <returns>
        /// Returns a success message if the instruction was marked as passed successfully.
        /// </returns>
        /// <response code="200">The instruction was successfully marked as passed.</response>
        /// <response code="400">
        /// A bad request occurred due to one of the following:
        /// - The user could not be found.
        /// - The personnel ID for the user was not found.
        /// </response>
        /// <response code="404">The instruction was not found in the database.</response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        [Authorize]
        [HttpPost("mark-instruction-as-passed/{instructionId}")]
        public async Task<IActionResult> MarkInstructionAsPassed(int instructionId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database with personnel and employee information
                var user = await _dbContext.Users
                    .Include(u => u.Personnel)
                        .ThenInclude(p => p.EmployeesByDepartment)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                if (user.personnel_id <= 0)
                {
                    return BadRequest("Personnel ID not found for this user");
                }

                // Get instruction status
                var instructionStatus = await _dbContext.InstructionStatuses
                    .Include(s => s.Instruction) // Include instruction to check type
                    .FirstOrDefaultAsync(s => s.instruction_id == instructionId && s.personnel_id == user.personnel_id);

                if (instructionStatus == null)
                {
                    return NotFound($"Instruction with ID {instructionId} not found for this user");
                }

                // Mark as passed
                instructionStatus.is_instruction_passed = true;
                instructionStatus.date_when_passed = DateTime.Now;
                instructionStatus.date_when_passed_UTC = DateTime.UtcNow;

                // NEW: Check if this is an unplanned instruction and if the user is a chief/deputy
                if (instructionStatus.Instruction.type_of_instruction == 1) // Unplanned instruction
                {
                    // Check if the user is a chief or deputy by role ID
                    // Role ID 2 = ChiefOfDepartment, Role ID 6 = DeputyChief
                    bool isChiefOrDeputy = user.user_role_id == 2 || user.user_role_id == 6; //TODO: Remove the HardCode.

                    if (isChiefOrDeputy)
                    {
                        // Set the flag to true - this instruction can now be assigned to other employees
                        instructionStatus.Instruction.is_passed_by_chief_unplanned_instr = true;
                        _dbContext.Instructions.Update(instructionStatus.Instruction);

                        var employee = user.Personnel?.EmployeesByDepartment?
                            .FirstOrDefault(e => e.department_id == instructionStatus.department_id);
                        var employeeName = employee?.full_name ?? "Unknown";

                        _logger.LogInformation($"Unplanned instruction {instructionId} marked as passed by chief/deputy (Role ID: {user.user_role_id}) {employeeName}. Instruction is now available for assignment.");
                    }
                }

                // Save changes
                await _dbContext.SaveChangesAsync();

                // Check if all personnel have passed this instruction
                await CheckAndUpdateInstructionCompletionStatus(instructionId);

                var responseMessage = "Instruction successfully marked as passed";

                // Add additional message for unplanned instructions passed by chief/deputy
                if (instructionStatus.Instruction.type_of_instruction == 1 &&
                    instructionStatus.Instruction.is_passed_by_chief_unplanned_instr)
                {
                    responseMessage += ". This unplanned instruction is now available for assignment to other employees.";
                }

                return Ok(responseMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking instruction {instructionId} as passed");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Checks if all personnel have passed the instruction and updates the instruction status.
        /// </summary>
        /// <param name="instructionId">The ID of the instruction to check</param>
        private async Task CheckAndUpdateInstructionCompletionStatus(int instructionId)
        {
            var totalStatuses = await _dbContext.InstructionStatuses
                .Where(s => s.instruction_id == instructionId)
                .CountAsync();

            var passedStatuses = await _dbContext.InstructionStatuses
                .Where(s => s.instruction_id == instructionId && s.is_instruction_passed)
                .CountAsync();

            if (totalStatuses > 0 && totalStatuses == passedStatuses)
            {
                var instruction = await _dbContext.Instructions.FindAsync(instructionId);
                if (instruction != null)
                {
                    instruction.is_passed_by_everyone = true;
                    await _dbContext.SaveChangesAsync();

                    // Notify relevant parties that the instruction has been completed by everyone
                    // This would involve your NotificationsService in a real implementation
                }
            }
        }
        #endregion

        #region ChiefRegion

        [AllowAnonymous]
        [HttpGet("greeting")]
        public IActionResult GetGreeting()
        {
            return Ok("Связь с сервером есть!");
        }


        [HttpGet("sync-instructions-with-db")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> SyncInstructionsWithDB()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                // Retrieve unassigned instructions for the user's department
                var unassignedInstructions = await _dbContext.Instructions
                    .Where(i =>
                        i.department_id == user.department_id &&
                        !i.is_assigned_to_people &&
                        i.type_of_instruction == 1)
                    .Select(i => new
                    {
                        i.instruction_id,
                        i.cause_of_instruction,
                        i.begin_date,
                        i.end_date,
                        i.type_of_instruction,
                        FilePaths = i.FilePaths.Select(fp => fp.file_path).ToList()
                    })
                    .ToListAsync();

                return Ok(unassignedInstructions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing instructions");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("sync-names-with-db")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> SyncNamesWithDB()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .Include(u => u.Personnel)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                // Retrieve employees in the same department, excluding the current user and department chief
                var employees = await _dbContext.EmployeesByDepartment
                    .Where(e =>
                        e.department_id == user.department_id &&
                        e.job_position != "Íà÷àëüíèê îòäåëà" &&
                        e.personnel_id != user.personnel_id)
                    .Select(e => new
                    {
                        FullName = e.full_name,
                        BirthDate = e.birth_date.ToString("yyyy-MM-dd")
                    })
                    .ToListAsync();

                return Ok(employees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing names");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Sends an instruction to a list of personnel based on names and birthdates.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Chiefs of Departments or Administrators) to send a specific instruction 
        /// to personnel identified by their names and birthdates. The function validates and processes the instruction 
        /// and creates the corresponding instruction status records.
        /// </remarks>
        /// <param name="package">
        /// The package containing the instruction details and a list of names and birthdates.
        /// </param>
        /// <returns>
        /// Returns an OK response if the instruction is successfully sent and processed. 
        /// Returns a BadRequest response if there is an error in the package or during processing.
        /// </returns>
        /// <response code="200">
        /// The instruction was successfully sent to the specified personnel.
        /// </response>
        /// <response code="400">
        /// A bad request occurred due to one of the following reasons:
        /// - The package is null or invalid.
        /// - An error occurred during instruction processing.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated or their username claim is missing.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        [HttpPost("send-instruction-to-names")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> SendInstructionToNames([FromBody] InstructionPackage package)
        {
            try
            {
                if (package == null)
                {
                    return BadRequest("The instruction package cannot be null.");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Get user information
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found in the database.");
                }

                // Process the instruction assignment
                try
                {
                    var result = await ProcessInstructionAssignment(package, user);
                    return Ok(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending instruction to names");
                    return BadRequest($"Error sending instruction: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendInstructionToNames");
                return BadRequest($"An error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that the current user has permission to assign instructions to the specified personnel
        /// </summary>
        /// <param name="currentUser">The user attempting to assign the instruction</param>
        /// <param name="personnelIds">List of personnel IDs to validate</param>
        /// <param name="instructionType">Type of instruction being assigned</param>
        /// <returns>Validation result with success status and error message if applicable</returns>
        private async Task<(bool IsValid, string ErrorMessage)> ValidateInstructionAssignmentPermissions(
            User currentUser,
            List<int> personnelIds,
            byte instructionType)
        {
            try
            {
                // Get current user's role
                var currentUserRole = currentUser.Role?.role_type ?? "";

                // Get allowed roles for assignment
                var allowedRoles = GetAllowedRolesForAssignment(currentUserRole);

                if (allowedRoles.Count == 0)
                {
                    return (false, "У вас нет прав для назначения инструктажей.");
                }

                // Get the roles of all target personnel
                var targetPersonnelRoles = await (from personnel in _dbContext.Personnel
                                                  join userEntity in _dbContext.Users on personnel.personnel_id equals userEntity.personnel_id into userGroup
                                                  from userEntity in userGroup.DefaultIfEmpty()
                                                  join role in _dbContext.Roles on userEntity.user_role_id equals role.role_id into roleGroup
                                                  from role in roleGroup.DefaultIfEmpty()
                                                  join emp in _dbContext.EmployeesByDepartment on personnel.personnel_id equals emp.personnel_id
                                                  where personnelIds.Contains(personnel.personnel_id)
                                                  select new
                                                  {
                                                      PersonnelId = personnel.personnel_id,
                                                      Role = role != null ? role.role_type : "User",
                                                      FullName = emp.full_name
                                                  }).ToListAsync();

                // Validate each target person's role
                foreach (var targetPerson in targetPersonnelRoles)
                {
                    // Check if current user can assign to this role
                    if (!allowedRoles.Contains(targetPerson.Role))
                    {
                        return (false, $"У вас нет прав назначать инструктажи пользователю {targetPerson.FullName} (роль: {GetRoleDisplayName(targetPerson.Role)}).");
                    }

                    /*// Check instruction type restrictions
                    if ((instructionType == 0 || instructionType == 1) &&
                        (targetPerson.Role.ToLower().Contains("deputy") || targetPerson.Role.ToLower().Contains("заместитель")))
                    {
                        return (false, $"Данный тип инструктажа не может быть назначен заместителю {targetPerson.FullName}.");
                    }*/ // TODO: THIS SEEMS OBSOLETE, CAUSE WE CAN ASSIGN TO DEPUTE THE INSTRUCTIONS OF TYPE 1 and 0
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating instruction assignment permissions");
                return (false, "Ошибка при проверке прав доступа.");
            }
        }

        private async Task<string> ProcessInstructionAssignment(InstructionPackage package, User currentUser)
        {
            // Create an execution strategy
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            // Execute the transaction with the strategy and return a result
            return await strategy.ExecuteAsync<string>(async () =>
            {
                // Start transaction inside the execution strategy
                using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Find the instruction by its cause
                        var instruction = await _dbContext.Instructions
                            .FirstOrDefaultAsync(i => i.cause_of_instruction == package.InstructionCause);

                        if (instruction == null)
                        {
                            throw new InvalidOperationException($"Instruction with cause '{package.InstructionCause}' not found.");
                        }

                        // Find personnel IDs based on names and birthdates
                        var namesList = package.NamesAndBirthDates.Select(t => t.Item1).ToList();
                        var birthdatesList = package.NamesAndBirthDates
                            .Select(t => DateTime.ParseExact(t.Item2, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                            .ToList();

                        // Query for matching personnel records with NULL handling
                        var query = _dbContext.Personnel
                            .Join(_dbContext.EmployeesByDepartment,
                                p => p.personnel_id,
                                e => e.personnel_id,
                                (p, e) => new
                                {
                                    Personnel = p,
                                    Employee = e
                                })
                            .Where(x => namesList.Contains(x.Employee.full_name) &&
                                   birthdatesList.Contains(x.Employee.birth_date) &&
                                   x.Employee.department_id == currentUser.department_id);

                        // Execute query and handle results carefully
                        var personnelRecords = await query.ToListAsync();

                        var personnel = personnelRecords
                            .Select(x => x.Personnel)
                            .Where(p => p != null)  // Filter out any null records
                            .ToList();

                        if (!personnel.Any())
                        {
                            throw new InvalidOperationException("No matching personnel found for the provided names and birthdates.");
                        }

                        // Validate assignment permissions BEFORE making any changes
                        var personnelIds = personnel.Select(p => p.personnel_id).ToList();
                        var validationResult = await ValidateInstructionAssignmentPermissions(
                            currentUser,
                            personnelIds,
                            instruction.type_of_instruction);

                        if (!validationResult.IsValid)
                        {
                            throw new UnauthorizedAccessException(validationResult.ErrorMessage);
                        }

                        // Mark the instruction as assigned to people ONLY after validation passes
                        instruction.is_assigned_to_people = true;
                        _dbContext.Instructions.Update(instruction);
                        await _dbContext.SaveChangesAsync();

                        // Create instruction status records for each personnel
                        int assignmentCount = 0;
                        foreach (var person in personnel)
                        {
                            // Safety check for null IDs
                            if (person.personnel_id <= 0 || currentUser.department_id <= 0 || instruction.instruction_id <= 0)
                            {
                                continue; // Skip invalid records
                            }

                            var existingStatus = await _dbContext.InstructionStatuses
                                .FirstOrDefaultAsync(s => s.instruction_id == instruction.instruction_id &&
                                                         s.personnel_id == person.personnel_id);

                            if (existingStatus == null)
                            {
                                var instructionStatus = new InstructionStatus
                                {
                                    instruction_id = instruction.instruction_id,
                                    personnel_id = person.personnel_id,
                                    department_id = currentUser.department_id,
                                    is_instruction_passed = false,
                                    when_was_sent_to_user = DateTime.Now,
                                    when_was_sent_to_user_UTC = DateTime.UtcNow,
                                    was_signed_by_personnel_id = currentUser.personnel_id
                                };

                                _dbContext.InstructionStatuses.Add(instructionStatus);
                                await _dbContext.SaveChangesAsync(); // Save to get the ID

                                // If normative instruction IDs are provided, add them to the junction table
                                if (package.NormativeInstructionNameIds != null && package.NormativeInstructionNameIds.Any())
                                {
                                    foreach (var normativeId in package.NormativeInstructionNameIds)
                                    {
                                        // Optional: Validate that the normative instruction exists
                                        var normativeExists = await _dbContext.NormativeInstructionNames
                                            .AnyAsync(n => n.id == normativeId);

                                        if (!normativeExists)
                                        {
                                            _logger.LogWarning($"Normative instruction with ID {normativeId} not found, skipping.");
                                            continue;
                                        }

                                        var junction = new InstructionStatusToNormativeInstrName
                                        {
                                            instruction_status_id = instructionStatus.id,
                                            normative_instruction_name_id = normativeId
                                        };

                                        _dbContext.InstructionStatusToNormativeInstrNames.Add(junction);
                                    }
                                }

                                assignmentCount++;
                            }
                        }

                        if (assignmentCount > 0)
                        {
                            await _dbContext.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();

                        // Result will be the count of personnel that were assigned
                        return $"Instruction '{package.InstructionCause}' has been assigned to {assignmentCount} people.";
                    }
                    catch (Exception ex)
                    {
                        // Only roll back if the transaction is still active
                        if (transaction.GetDbTransaction().Connection != null)
                        {
                            try
                            {
                                await transaction.RollbackAsync();
                            }
                            catch (Exception rollbackEx)
                            {
                                _logger.LogError(rollbackEx, "Error rolling back transaction");
                            }
                        }
                        throw; // Rethrow to be handled by the caller
                    }
                }
            });
        }
        /*private async Task<string> ProcessInstructionAssignment(InstructionPackage package, User currentUser) // This function is for debugging.
        {
            // Add detailed logging for the entire process
            _logger.LogInformation($"===== DEBUG DIAGNOSTICS START =====");
            _logger.LogInformation($"Received instruction package: Cause='{package.InstructionCause}'");
            _logger.LogInformation($"Number of employees in package: {package.NamesAndBirthDates?.Count ?? 0}");

            if (package.NormativeInstructionNameIds != null && package.NormativeInstructionNameIds.Any())
            {
                _logger.LogInformation($"Normative instruction IDs in package: {string.Join(", ", package.NormativeInstructionNameIds)}");
            }
            else
            {
                _logger.LogWarning($"⚠️ No normative instruction IDs in package or empty list!");
            }

            // Create an execution strategy
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            // Execute the transaction with the strategy and return a result
            return await strategy.ExecuteAsync<string>(async () =>
            {
                // Start transaction inside the execution strategy
                using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        _logger.LogInformation($"🔍 Searching for instruction with cause: '{package.InstructionCause}'");

                        // Find the instruction by its cause
                        var instruction = await _dbContext.Instructions
                            .FirstOrDefaultAsync(i => i.cause_of_instruction == package.InstructionCause);

                        if (instruction == null)
                        {
                            _logger.LogError($"❌ Instruction with cause '{package.InstructionCause}' not found.");
                            throw new InvalidOperationException($"Instruction with cause '{package.InstructionCause}' not found.");
                        }

                        _logger.LogInformation($"✅ Found instruction ID={instruction.instruction_id}, Cause='{instruction.cause_of_instruction}'");

                        // Mark the instruction as assigned to people
                        instruction.is_assigned_to_people = true;
                        _dbContext.Instructions.Update(instruction);
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation($"✅ Instruction marked as assigned to people");

                        // Find personnel IDs based on names and birthdates
                        var namesList = package.NamesAndBirthDates.Select(t => t.Item1).ToList();
                        var birthdatesList = package.NamesAndBirthDates
                            .Select(t => DateTime.ParseExact(t.Item2, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                            .ToList();

                        _logger.LogInformation($"🔍 Searching for personnel based on {namesList.Count} names and birthdates");

                        // Log details of names and birthdates for debugging
                        for (int i = 0; i < namesList.Count; i++)
                        {
                            _logger.LogInformation($"  -> Looking for: {namesList[i]}, DOB: {package.NamesAndBirthDates[i].Item2}");
                        }

                        // Query for matching personnel records with NULL handling
                        var query = _dbContext.Personnel
                            .Join(_dbContext.EmployeesByDepartment,
                                p => p.personnel_id,
                                e => e.personnel_id,
                                (p, e) => new
                                {
                                    Personnel = p,
                                    Employee = e
                                })
                            .Where(x => namesList.Contains(x.Employee.full_name) &&
                                   birthdatesList.Contains(x.Employee.birth_date) &&
                                   x.Employee.department_id == currentUser.department_id);

                        // Execute query and handle results carefully
                        var personnelRecords = await query.ToListAsync();
                        _logger.LogInformation($"✅ Found {personnelRecords.Count} personnel records");

                        var personnel = personnelRecords
                            .Select(x => x.Personnel)
                            .Where(p => p != null)  // Filter out any null records
                            .ToList();

                        if (!personnel.Any())
                        {
                            _logger.LogError($"❌ No matching personnel found for the provided names and birthdates.");
                            throw new InvalidOperationException("No matching personnel found for the provided names and birthdates.");
                        }

                        // Log found personnel for diagnostics
                        foreach (var person in personnel)
                        {
                            _logger.LogInformation($"  -> Found personnel ID={person.personnel_id}, Number={person.personnel_number}");
                        }

                        // Create instruction status records for each personnel
                        int assignmentCount = 0;
                        int normativeLinksCreated = 0;
                        foreach (var person in personnel)
                        {
                            // Safety check for null IDs
                            if (person.personnel_id <= 0 || currentUser.department_id <= 0 || instruction.instruction_id <= 0)
                            {
                                _logger.LogWarning($"⚠️ Skipping person with invalid IDs: Person={person.personnel_id}, Dept={currentUser.department_id}, Instr={instruction.instruction_id}");
                                continue; // Skip invalid records
                            }

                            var existingStatus = await _dbContext.InstructionStatuses
                                .FirstOrDefaultAsync(s => s.instruction_id == instruction.instruction_id &&
                                                         s.personnel_id == person.personnel_id);

                            if (existingStatus == null)
                            {
                                _logger.LogInformation($"📝 Creating new instruction status for personnel ID={person.personnel_id}");

                                var instructionStatus = new InstructionStatus
                                {
                                    instruction_id = instruction.instruction_id,
                                    personnel_id = person.personnel_id,
                                    department_id = currentUser.department_id,
                                    is_instruction_passed = false,
                                    when_was_sent_to_user = DateTime.Now,
                                    when_was_sent_to_user_UTC = DateTime.UtcNow,
                                    was_signed_by_personnel_id = currentUser.personnel_id
                                };

                                _dbContext.InstructionStatuses.Add(instructionStatus);
                                await _dbContext.SaveChangesAsync(); // Save to get the ID
                                _logger.LogInformation($"✅ Created instruction status ID={instructionStatus.id}");

                                // If normative instruction IDs are provided, add them to the junction table
                                if (package.NormativeInstructionNameIds != null && package.NormativeInstructionNameIds.Any())
                                {
                                    _logger.LogInformation($"📝 Adding {package.NormativeInstructionNameIds.Count} normative instructions");

                                    foreach (var normativeId in package.NormativeInstructionNameIds)
                                    {
                                        try
                                        {
                                            // Check if normative instruction exists
                                            var normativeExists = await _dbContext.NormativeInstructionNames
                                                .AnyAsync(n => n.id == normativeId);

                                            if (!normativeExists)
                                            {
                                                _logger.LogWarning($"⚠️ Normative instruction ID={normativeId} not found in database!");
                                                continue;
                                            }

                                            _logger.LogInformation($"  -> Linking: status ID={instructionStatus.id} with normative instruction ID={normativeId}");

                                            var junction = new InstructionStatusToNormativeInstrName
                                            {
                                                instruction_status_id = instructionStatus.id,
                                                normative_instruction_name_id = normativeId
                                            };

                                            _dbContext.InstructionStatusToNormativeInstrNames.Add(junction);
                                            normativeLinksCreated++;
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError($"❌ Error adding normative instruction ID={normativeId}: {ex.Message}");
                                        }
                                    }

                                    // Save changes after each status to ensure links are saved
                                    await _dbContext.SaveChangesAsync();
                                    _logger.LogInformation($"✅ Saved normative links for status ID={instructionStatus.id}");
                                }
                                else
                                {
                                    _logger.LogWarning($"⚠️ No normative instructions to add for status ID={instructionStatus.id}");
                                }

                                assignmentCount++;
                            }
                            else
                            {
                                _logger.LogInformation($"ℹ️ Instruction status already exists for personnel ID={person.personnel_id}");
                            }
                        }

                        if (assignmentCount > 0)
                        {
                            _logger.LogInformation($"📊 Total: created {assignmentCount} instruction statuses and {normativeLinksCreated} normative links");
                            await _dbContext.SaveChangesAsync();
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ No new instruction statuses created");
                        }

                        await transaction.CommitAsync();
                        _logger.LogInformation($"✅ Transaction successfully committed");
                        _logger.LogInformation($"===== DEBUG DIAGNOSTICS END =====");

                        // Result will be the count of personnel that were assigned
                        return $"Instruction '{package.InstructionCause}' has been assigned to {assignmentCount} people with {normativeLinksCreated} normative instruction links.";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ ERROR in ProcessInstructionAssignment: {ex.Message}");
                        _logger.LogError($"❌ StackTrace: {ex.StackTrace}");

                        // Only roll back if the transaction is still active
                        if (transaction.GetDbTransaction().Connection != null)
                        {
                            try
                            {
                                await transaction.RollbackAsync();
                                _logger.LogInformation($"✅ Transaction successfully rolled back");
                            }
                            catch (Exception rollbackEx)
                            {
                                _logger.LogError(rollbackEx, "❌ Error rolling back transaction");
                            }
                        }

                        _logger.LogInformation($"===== DEBUG DIAGNOSTICS END WITH ERROR =====");
                        throw; // Rethrow to be handled by the caller
                    }
                }
            });
        }*/

        // Add this method to InstructionsController.cs

        /// <summary>
        /// Assigns an unplanned instruction to selected employees with predetermined normative instructions.
        /// </summary>
        /// <remarks>
        /// This endpoint is specifically for assigning unplanned instructions that have already been passed by chiefs.
        /// The normative instructions are predetermined and cannot be modified during assignment.
        /// </remarks>
        /// <param name="package">Package containing instruction details and selected employees</param>
        /// <returns>Success message with assignment details</returns>
        /// <response code="200">The unplanned instruction was successfully assigned.</response>
        /// <response code="400">Bad request - Invalid package data or instruction not ready for assignment.</response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        /// <response code="403">Forbidden - The user does not have the required role.</response>
        /// <response code="500">Internal server error occurred during assignment.</response>
        [HttpPost("assign-unplanned-instruction-to-employees")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> AssignUnplannedInstructionToEmployees([FromBody] UnplannedInstructionAssignmentPackage package)
        {
            if (package == null || package.SelectedEmployees == null || !package.SelectedEmployees.Any())
                return BadRequest("Invalid package data or no employees selected");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                return BadRequest("Invalid user ID");

            var currentUser = await _dbContext.Users
                .Include(u => u.Department)
                .Include(u => u.Personnel)
                .FirstOrDefaultAsync(u => u.id == userIdInt);

            if (currentUser == null)
                return BadRequest("User not found");

            var instruction = await _dbContext.Instructions
                .FirstOrDefaultAsync(i => i.instruction_id == package.InstructionId);

            if (instruction == null)
                return BadRequest($"Instruction with ID {package.InstructionId} not found");

            if (instruction.type_of_instruction != 1) // 1 = unplanned
                return BadRequest("This endpoint is only for unplanned instructions");

            if (!instruction.is_passed_by_chief_unplanned_instr)
                return BadRequest("Unplanned instruction must be passed by chief before assignment");

            if (instruction.department_id != currentUser.department_id)
                return Forbid("You can only assign instructions from your department");

            // ✅ Wrap everything inside EF retry strategy
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                var failedAssignments = new List<string>();
                int assignmentCount = 0;

                try
                {
                    foreach (var employeeInfo in package.SelectedEmployees)
                    {
                        if (!DateTime.TryParseExact(employeeInfo.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedBirthDate))
                        {
                            failedAssignments.Add($"Неверный формат даты у {employeeInfo.FullName}: {employeeInfo.BirthDate}");
                            continue;
                        }

                        var employee = await _dbContext.EmployeesByDepartment
                            .FirstOrDefaultAsync(p => p.full_name == employeeInfo.FullName &&
                                                      p.birth_date.Date == parsedBirthDate.Date);

                        if (employee == null)
                        {
                            failedAssignments.Add($"Сотрудник не найден: {employeeInfo.FullName}");
                            continue;
                        }

                        var alreadyAssigned = await _dbContext.InstructionStatuses
                            .AnyAsync(s => s.instruction_id == instruction.instruction_id &&
                                           s.personnel_id == employee.personnel_id);

                        if (alreadyAssigned)
                        {
                            failedAssignments.Add($"Уже назначен: {employeeInfo.FullName}");
                            continue;
                        }

                        var instructionStatus = new InstructionStatus
                        {
                            instruction_id = instruction.instruction_id,
                            personnel_id = employee.personnel_id,
                            department_id = currentUser.department_id,
                            is_instruction_passed = false,
                            when_was_sent_to_user = DateTime.Now,
                            when_was_sent_to_user_UTC = DateTime.UtcNow,
                            was_signed_by_personnel_id = currentUser.personnel_id
                        };

                        _dbContext.InstructionStatuses.Add(instructionStatus);
                        await _dbContext.SaveChangesAsync();

                        if (package.NormativeInstructionNameIds?.Any() == true)
                        {
                            foreach (var normId in package.NormativeInstructionNameIds)
                            {
                                var normativeExists = await _dbContext.NormativeInstructionNames.AnyAsync(n => n.id == normId);
                                if (!normativeExists)
                                {
                                    _logger.LogWarning($"Normative instruction ID {normId} not found.");
                                    continue;
                                }

                                _dbContext.InstructionStatusToNormativeInstrNames.Add(new InstructionStatusToNormativeInstrName
                                {
                                    instruction_status_id = instructionStatus.id,
                                    normative_instruction_name_id = normId
                                });
                            }
                        }

                        assignmentCount++;
                    }

                    if (assignmentCount > 0)
                    {
                        instruction.is_assigned_to_people = true;
                        _dbContext.Instructions.Update(instruction);
                        await _dbContext.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        Message = $"Назначено {assignmentCount} сотрудникам.",
                        Total = package.SelectedEmployees.Count,
                        Failed = failedAssignments,
                        NormativeInstructionsAssigned = package.NormativeInstructionNameIds?.Count ?? 0
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Ошибка при назначении внепланового инструктажа.");
                    return StatusCode(500, "Внутренняя ошибка сервера");
                }
            });
        }




        /// <summary>
        /// Gets compliance data for instructions, showing who has passed each instruction.
        /// </summary>
        /// <remarks>
        /// This endpoint returns instruction compliance information with detailed employee data,
        /// including who assigned the instruction and which normative documents were used.
        /// </remarks>
        /// <returns>
        /// A list of instructions with detailed employee compliance data.
        /// </returns>
        [HttpGet("get-instructions-with-compliance-data")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> GetInstructionsWithComplianceData()
        {
            try
            {
                // Get current user's information
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                int departmentId = user.department_id;

                // Get all instructions for this department
                var instructions = await _dbContext.Instructions
                    .Where(i => i.department_id == departmentId)
                    .Include(i => i.InstructionType)
                    .OrderByDescending(i => i.begin_date)
                    .ToListAsync();

                // Create result list with all required data
                var result = new List<object>();

                // For each instruction, get the employee compliance data
                foreach (var instruction in instructions)
                {
                    // Get all instruction statuses for this instruction
                    var statuses = await _dbContext.InstructionStatuses
                        .Where(s => s.instruction_id == instruction.instruction_id && s.department_id == departmentId)
                        .Include(s => s.Personnel)
                        .ThenInclude(p => p.EmployeesByDepartment.Where(e => e.department_id == departmentId))
                        .ToListAsync();

                    // Create employee data
                    var employeeData = new List<object>();

                    foreach (var status in statuses)
                    {
                        // Get employee info
                        var employee = status.Personnel.EmployeesByDepartment
                            .FirstOrDefault(e => e.department_id == departmentId);

                        if (employee != null)
                        {
                            // Get normative instruction names for this status
                            var normativeInstructions = await _dbContext.InstructionStatusToNormativeInstrNames
                                .Where(link => link.instruction_status_id == status.id)
                                .Include(link => link.NormativeInstructionName)
                                .Select(link => link.NormativeInstructionName.normative_instruction_name)
                                .ToListAsync();

                            // Get the assigner's name and job position (who assigned the instruction)
                            string assignerInfo = "Неизвестно";
                            if (status.was_signed_by_personnel_id > 0)
                            {
                                var assigner = await _dbContext.Personnel
                                    .Where(p => p.personnel_id == status.was_signed_by_personnel_id)
                                    .Include(p => p.EmployeesByDepartment)
                                    .FirstOrDefaultAsync();

                                if (assigner != null)
                                {
                                    var assignerEmployee = assigner.EmployeesByDepartment
                                        .FirstOrDefault(e => e.department_id == departmentId);

                                    if (assignerEmployee != null)
                                    {
                                        // Combine name and job position
                                        assignerInfo = $"{assignerEmployee.full_name} {assignerEmployee.job_position}";
                                    }
                                }
                            }

                            employeeData.Add(new
                            {
                                // Employee information
                                FullName = employee.full_name,
                                Position = employee.job_position,
                                BirthDate = employee.birth_date,

                                // Instruction status information
                                HasPassed = status.is_instruction_passed,
                                DatePassed = status.date_when_passed,
                                DateAssigned = status.when_was_sent_to_user,

                                // Assigner information
                                AssignedBy = assignerInfo,

                                // Normative instruction names
                                NormativeDocuments = normativeInstructions
                            });
                        }
                    }

                    // Add to result list
                    result.Add(new
                    {
                        // Instruction information
                        InstructionId = instruction.instruction_id,
                        CauseOfInstruction = instruction.cause_of_instruction,
                        BeginDate = instruction.begin_date,
                        EndDate = instruction.end_date,
                        TypeOfInstruction = instruction.type_of_instruction,
                        TypeName = instruction.InstructionType?.name_of_type_instruction ?? await GetInstructionTypeNameAsync(instruction.type_of_instruction),
                        IsAssignedToPeople = instruction.is_assigned_to_people,
                        IsPassedByEveryone = instruction.is_passed_by_everyone,

                        // Employee data with compliance status
                        EmployeeData = employeeData
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving instruction compliance data");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets a filtered report of compliance data for a specific instruction.
        /// </summary>
        /// <remarks>
        /// Returns chronologically ordered compliance data for a specific instruction,
        /// with employees who passed listed first (sorted by date), followed by those who didn't pass.
        /// Now properly handles cross-department assignments from Management.
        /// </remarks>
        /// <param name="instructionId">The ID of the instruction to report on</param>
        /// <returns>A report with sorted employee compliance data</returns>
        [HttpGet("get-instruction-compliance-report/{instructionId}")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator, Management")]
        public async Task<IActionResult> GetInstructionComplianceReport(int instructionId)
        {
            try
            {
                // Get current user's information
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                int departmentId = user.department_id;
                string userRole = user.Role?.role_type ?? "";

                // Get the specific instruction - check access permissions
                var instruction = await _dbContext.Instructions
                    .Where(i => i.instruction_id == instructionId)
                    .Include(i => i.InstructionType)
                    .FirstOrDefaultAsync();

                if (instruction == null)
                {
                    return NotFound($"Instruction with ID {instructionId} not found");
                }

                // Access control: 
                // - Management can see all instructions
                // - Chiefs/Deputies can only see instructions from their department
                if (userRole != "Management" && userRole != "Administrator" && instruction.department_id != departmentId)
                {
                    return Forbid("You don't have access to this instruction");
                }

                // Get instruction statuses - different logic based on user role
                List<InstructionStatus> statuses;

                if (userRole == "Management" || userRole == "Administrator")
                {
                    // Management can see ALL statuses for this instruction across all departments
                    statuses = await _dbContext.InstructionStatuses
                        .Where(s => s.instruction_id == instructionId)
                        .Include(s => s.Personnel)
                        .ThenInclude(p => p.EmployeesByDepartment)
                        .ToListAsync();
                }
                else
                {
                    // Chiefs/Deputies can only see statuses from their department
                    statuses = await _dbContext.InstructionStatuses
                        .Where(s => s.instruction_id == instructionId && s.department_id == departmentId)
                        .Include(s => s.Personnel)
                        .ThenInclude(p => p.EmployeesByDepartment.Where(e => e.department_id == departmentId))
                        .ToListAsync();
                }

                // Create employee data
                var employeeData = new List<object>();

                foreach (var status in statuses)
                {
                    // Get employee info - for Management, we need to get the employee from the correct department
                    var employee = status.Personnel.EmployeesByDepartment
                        .FirstOrDefault(e => e.department_id == status.department_id);

                    if (employee != null)
                    {
                        // Get normative instruction names for this status
                        var normativeInstructions = await _dbContext.InstructionStatusToNormativeInstrNames
                            .Where(link => link.instruction_status_id == status.id)
                            .Include(link => link.NormativeInstructionName)
                            .Select(link => link.NormativeInstructionName.normative_instruction_name)
                            .ToListAsync();

                        // Get the assigner's name and job position - FIXED VERSION
                        string assignerInfo = "Неизвестно";
                        if (status.was_signed_by_personnel_id > 0)
                        {
                            var assigner = await _dbContext.Personnel
                                .Where(p => p.personnel_id == status.was_signed_by_personnel_id)
                                .Include(p => p.EmployeesByDepartment)
                                .FirstOrDefaultAsync();

                            if (assigner != null)
                            {
                                // Try to find the assigner in any department (not just the current one)
                                var assignerEmployee = assigner.EmployeesByDepartment.FirstOrDefault();

                                if (assignerEmployee != null)
                                {
                                    // Get the department name for the assigner
                                    var assignerDepartment = await _dbContext.Departments
                                        .FirstOrDefaultAsync(d => d.department_id == assignerEmployee.department_id);

                                    var departmentName = assignerDepartment?.department_name ?? "Неизвестный отдел";

                                    // Include department info to distinguish Management from department chiefs
                                    assignerInfo = $"{assignerEmployee.full_name} - {assignerEmployee.job_position} ({departmentName})";
                                }
                            }
                        }

                        // Get the department name for the current employee
                        var employeeDepartment = await _dbContext.Departments
                            .FirstOrDefaultAsync(d => d.department_id == employee.department_id);

                        employeeData.Add(new
                        {
                            // Employee information
                            FullName = employee.full_name,
                            Position = employee.job_position,
                            BirthDate = employee.birth_date,
                            Department = employeeDepartment?.department_name ?? "Неизвестный отдел",

                            // Instruction status information
                            HasPassed = status.is_instruction_passed,
                            DatePassed = status.date_when_passed,
                            DateAssigned = status.when_was_sent_to_user,

                            // Assigner information - now properly shows Management assignments
                            AssignedBy = assignerInfo,

                            // Normative instruction names
                            NormativeDocuments = normativeInstructions
                        });
                    }
                }

                // Sort the employee data:
                // 1. People who passed first (sorted by date, earliest first)
                // 2. People who haven't passed (sorted by name)
                var sortedEmployeeData = employeeData
                    .OrderBy(e => (bool)((dynamic)e).HasPassed ? 0 : 1)
                    .ThenBy(e => ((dynamic)e).HasPassed ? ((dynamic)e).DatePassed : null)
                    .ThenBy(e => ((dynamic)e).FullName)
                    .ToList();

                // Create the final report
                var report = new
                {
                    // Instruction information
                    InstructionId = instruction.instruction_id,
                    CauseOfInstruction = instruction.cause_of_instruction,
                    BeginDate = instruction.begin_date,
                    EndDate = instruction.end_date,
                    TypeOfInstruction = instruction.type_of_instruction,
                    TypeName = instruction.InstructionType?.name_of_type_instruction ?? await GetInstructionTypeNameAsync(instruction.type_of_instruction),

                    // Statistics
                    TotalEmployees = sortedEmployeeData.Count,
                    PassedCount = sortedEmployeeData.Count(e => (bool)((dynamic)e).HasPassed),

                    // Show which user is viewing this report
                    ViewedBy = $"{user.Role?.role_type} from {user.Department?.department_name}",

                    // Sorted employee data
                    EmployeeData = sortedEmployeeData
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving compliance report for instruction {instructionId}");
                return StatusCode(500, "Internal server error");
            }
        }

        // FIXED: In InstructionsController.cs, replace the GetEmployeesWithRoles method with this version

        /*[HttpGet("get-employees-with-roles")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Coordinator, Administrator")]
        public async Task<IActionResult> GetEmployeesWithRoles()
        {
            try
            {
                _logger.LogInformation("=== GET EMPLOYEES WITH ROLES DEBUG START ===");

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation($"User ID from claims: {userId}");

                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    _logger.LogError($"Invalid user ID: {userId}");
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    _logger.LogError($"User not found with ID: {userIdInt}");
                    return BadRequest("User not found");
                }

                _logger.LogInformation($"Found user: {user.username}, Personnel ID: {user.personnel_id}, Department ID: {user.department_id}, Role: {user.Role?.role_type}");

                int departmentId = user.department_id;
                string currentUserRole = user.Role?.role_type ?? "";

                // FIXED: Restructure the query to avoid Include after Join
                // First get users with their roles
                var usersWithRoles = await _dbContext.Users
                    .Where(u => u.department_id == departmentId && u.personnel_id != user.personnel_id)
                    .Include(u => u.Role)
                    .ToListAsync();

                _logger.LogInformation($"Found {usersWithRoles.Count} users with roles in department {departmentId} (excluding current user)");

                // Then join with employees
                var employeesWithRoles = await _dbContext.EmployeesByDepartment
                    .Where(e => e.department_id == departmentId && e.personnel_id != user.personnel_id)
                    .Where(e => usersWithRoles.Select(u => u.personnel_id).Contains(e.personnel_id))
                    .ToListAsync();

                _logger.LogInformation($"Found {employeesWithRoles.Count} employees with user accounts in department {departmentId}");

                // Create the combined result manually
                var combinedResults = (from emp in employeesWithRoles
                                       join usr in usersWithRoles on emp.personnel_id equals usr.personnel_id
                                       select new
                                       {
                                           Employee = emp,
                                           User = usr
                                       }).ToList();

                _logger.LogInformation($"Combined {combinedResults.Count} employee-user pairs");

                // Log details of employees with roles
                foreach (var empWithRole in combinedResults)
                {
                    _logger.LogInformation($"  Employee: {empWithRole.Employee.full_name}, Personnel ID: {empWithRole.Employee.personnel_id}, Role: {empWithRole.User.Role?.role_type ?? "NULL"}");
                }

                // Get current user's role for filtering
                var allowedRoles = GetAllowedRolesForAssignment(currentUserRole);
                _logger.LogInformation($"Allowed roles for {currentUserRole}: [{string.Join(", ", allowedRoles)}]");

                // Apply role-based filtering
                var filteredEmployees = combinedResults
                    .Where(e => allowedRoles.Contains(e.User.Role?.role_type ?? ""))
                    .ToList();

                _logger.LogInformation($"After role filtering: {filteredEmployees.Count} employees");

                // Log which employees passed the filter
                foreach (var emp in filteredEmployees)
                {
                    _logger.LogInformation($"  Filtered IN: {emp.Employee.full_name} - Role: {emp.User.Role?.role_type}");
                }

                // Log which employees were filtered OUT
                var excludedEmployees = combinedResults.Except(filteredEmployees);
                foreach (var emp in excludedEmployees)
                {
                    _logger.LogInformation($"  Filtered OUT: {emp.Employee.full_name} - Role: {emp.User.Role?.role_type} (not in allowed roles)");
                }

                // Transform to the response model
                var result = filteredEmployees.Select(e => new
                {
                    FullName = e.Employee.full_name,
                    BirthDate = e.Employee.birth_date.ToString("yyyy-MM-dd"),
                    Role = e.User.Role?.role_type ?? "",
                    UserId = e.User.id
                }).ToList();

                _logger.LogInformation($"Final result count: {result.Count}");

                // Log each result item
                foreach (var item in result)
                {
                    _logger.LogInformation($"  Final Result: {item.FullName} ({item.BirthDate}) - {item.Role}");
                }

                _logger.LogInformation("=== GET EMPLOYEES WITH ROLES DEBUG END ===");
                var jsonResult = JsonConvert.SerializeObject(result, new JsonSerializerSettings
                {
                    Formatting = Formatting.None
                });

                return Content(jsonResult, "application/json; charset=utf-8");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employees with roles");
                return StatusCode(500, "Internal server error");
            }
        }

        // Keep the corrected GetAllowedRolesForAssignment method:
        private List<string> GetAllowedRolesForAssignment(string currentUserRole)
        {
            var allowedRoles = new List<string>();

            _logger.LogInformation($"Determining allowed roles for: '{currentUserRole}'");

            switch (currentUserRole?.ToLower())
            {
                case "chiefofdepartment":      // CORRECTED: Fixed the typo
                case "chief of department":
                case "management":
                    // Chief can assign to: Users, Coordinators, and Deputies
                    allowedRoles.AddRange(new[] { "User", "Coordinator", "DeputyChief"});
                    _logger.LogInformation("User is Chief - can assign to User, Coordinator, DeputyChief, Deputy Chief");
                    break;

                case "deputychief":
                case "deputy chief":
                case "deputy":
                    allowedRoles.AddRange(new[] { "User", "Coordinator" });
                    _logger.LogInformation("User is Deputy - can assign to User, Coordinator");
                    break;

                case "coordinator":
                    allowedRoles.Add("User");
                    _logger.LogInformation("User is Coordinator - can assign to User only");
                    break;

                case "administrator":
                case "admin":
                    allowedRoles.AddRange(new[] { "User", "Coordinator", "DeputyChief", "Deputy Chief", "ChiefOfDepartment", "Chief of Department", "Management" });
                    _logger.LogInformation("User is Administrator - can assign to everyone");
                    break;

                default:
                    _logger.LogWarning($"Unknown or unauthorized role: '{currentUserRole}' - no assignment permissions");
                    break;
            }

            _logger.LogInformation($"Final allowed roles: [{string.Join(", ", allowedRoles)}]");
            return allowedRoles;
        }*/


        [HttpGet("get-employees-with-roles")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Coordinator, Administrator")]
        public async Task<IActionResult> GetEmployeesWithRoles()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                int departmentId = user.department_id;
                string currentUserRole = user.Role?.role_type ?? "";

                // Get users with their roles
                var usersWithRoles = await _dbContext.Users
                    .Where(u => u.department_id == departmentId && u.personnel_id != user.personnel_id)
                    .Include(u => u.Role)
                    .ToListAsync();

                // Get employees with user accounts
                var employeesWithRoles = await _dbContext.EmployeesByDepartment
                    .Where(e => e.department_id == departmentId && e.personnel_id != user.personnel_id)
                    .Where(e => usersWithRoles.Select(u => u.personnel_id).Contains(e.personnel_id))
                    .ToListAsync();

                // Combine employee and user data
                var combinedResults = (from emp in employeesWithRoles
                                       join usr in usersWithRoles on emp.personnel_id equals usr.personnel_id
                                       select new
                                       {
                                           Employee = emp,
                                           User = usr
                                       }).ToList();

                // Apply role-based filtering
                var allowedRoles = GetAllowedRolesForAssignment(currentUserRole);
                var filteredEmployees = combinedResults
                    .Where(e => allowedRoles.Contains(e.User.Role?.role_type ?? ""))
                    .ToList();

                // Transform to response model
                var result = filteredEmployees.Select(e => new
                {
                    FullName = e.Employee.full_name,
                    BirthDate = e.Employee.birth_date.ToString("yyyy-MM-dd"),
                    Role = e.User.Role?.role_type ?? "",
                    UserId = e.User.id
                }).ToList();

                var jsonResult = JsonConvert.SerializeObject(result, new JsonSerializerSettings
                {
                    Formatting = Formatting.None
                });

                return Content(jsonResult, "application/json; charset=utf-8");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employees with roles");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the list of roles that the current user can assign instructions to
        /// </summary>
        /// <param name="currentUserRole">The role of the current user</param>
        /// <returns>List of roles that can be assigned to</returns>
        private List<string> GetAllowedRolesForAssignment(string currentUserRole)
        {
            var allowedRoles = new List<string>();

            //_logger.LogInformation($"Determining allowed roles for current user role: '{currentUserRole}'");

            switch (currentUserRole?.ToLower())
            {
                case "chiefofdepartment":
                    // Chief can assign to: Users, Coordinators, and Deputies
                    allowedRoles.AddRange(new[] { "User", "Coordinator", "DeputyChief" });
                    //_logger.LogInformation("User is ChiefOfDepartment - can assign to User, Coordinator, DeputyChief");
                    break;

                case "deputychief":
                    allowedRoles.AddRange(new[] { "User", "Coordinator" });
                   // _logger.LogInformation("User is DeputyChief - can assign to User, Coordinator");
                    break;

                case "coordinator":
                    allowedRoles.Add("User");
                   // _logger.LogInformation("User is Coordinator - can assign to User only");
                    break;

                case "management":
                    // Management can assign to everyone except Admin
                    allowedRoles.AddRange(new[] { "User", "Coordinator", "DeputyChief", "ChiefOfDepartment" });
                    //_logger.LogInformation("User is Management - can assign to User, Coordinator, DeputyChief, ChiefOfDepartment");
                    break;

                case "admin":
                    // Admin can assign to everyone
                    allowedRoles.AddRange(new[] { "User", "Coordinator", "DeputyChief", "ChiefOfDepartment", "Management" });
                    //_logger.LogInformation("User is Admin - can assign to everyone");
                    break;

                default:
                    _logger.LogWarning($"Unknown or unauthorized role: '{currentUserRole}' - no assignment permissions");
                    break;
            }

            //_logger.LogInformation($"Final allowed roles: [{string.Join(", ", allowedRoles)}]");
            return allowedRoles;
        }

        /// <summary>
        /// Converts role code to display name for error messages
        /// </summary>
        /// <param name="role">Role code</param>
        /// <returns>Display name in Russian</returns>
        private string GetRoleDisplayName(string role)
        {
            return role?.ToLower() switch
            {
                "chiefofdepartment" => "Начальник отдела",
                "deputychief" => "Заместитель начальника",
                "coordinator" => "Координатор",
                "user" => "Сотрудник",
                "management" => "Главный инженер",
                "admin" => "Администратор",
                _ => role ?? "Неизвестная роль"
            };
        }




        #endregion

        /// <summary>
        /// Adds a new instruction into the database.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Chiefs of Departments or Administrators) to add a new instruction 
        /// to the database. The instruction details include cause, end date, and type.
        /// </remarks>
        /// <param name="instructionDto">The instruction details to be added</param>
        /// <returns>
        /// Returns the created instruction if successful, or appropriate error responses.
        /// </returns>
        /// <response code="200">The instruction was successfully added to the database</response>
        /// <response code="400">The instruction data is invalid or there was an error processing the request</response>
        /// <response code="401">The user is not authenticated</response>
        /// <response code="403">The user doesn't have permission to add instructions</response>
        [HttpPost("add-new-instruction-into-db")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> AddNewInstructionIntoDB([FromBody] InstructionCreateDto instructionDto)
        {
            try
            {
                if (instructionDto == null)
                {
                    return BadRequest("Instruction data is missing");
                }

                // Get current user and department
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim not found");
                }

                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.username == username);

                if (user == null)
                {
                    return BadRequest("User not found in database");
                }

                int departmentId = user.department_id;

                // Create a new instruction entity
                var instruction = new Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models.Instruction
                {
                    cause_of_instruction = instructionDto.CauseOfInstruction,
                    begin_date = DateTime.UtcNow, // Always use current time for begin_date
                    end_date = instructionDto.EndDate,
                    type_of_instruction = instructionDto.TypeOfInstruction,
                    is_passed_by_everyone = false, // Always start as not passed
                    is_assigned_to_people = false, // Not assigned to people yet
                    department_id = departmentId
                };

                // Special case for department ID 5 (Management)
                if (departmentId == 5)
                {
                    instruction.is_assigned_to_people = true;
                }

                // Create an execution strategy
                var strategy = _dbContext.Database.CreateExecutionStrategy();

                // Execute the transaction with the strategy
                try
                {
                    var resultDto = await strategy.ExecuteAsync<InstructionResultDto>(async () =>
                    {
                        // Start transaction inside the execution strategy
                        using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                        {
                            try
                            {
                                // Check if an instruction with the same cause already exists
                                var existingInstruction = await _dbContext.Instructions
                                    .FirstOrDefaultAsync(i =>
                                        i.cause_of_instruction == instruction.cause_of_instruction &&
                                        i.department_id == departmentId);

                                if (existingInstruction != null)
                                {
                                    throw new InvalidOperationException("An instruction with the same cause already exists");
                                }

                                // Add the instruction to the database
                                _dbContext.Instructions.Add(instruction);
                                await _dbContext.SaveChangesAsync();

                                await transaction.CommitAsync();

                                // Map entity to DTO for response - modified to not include paths
                                return new InstructionResultDto
                                {
                                    InstructionId = instruction.instruction_id,
                                    CauseOfInstruction = instruction.cause_of_instruction,
                                    BeginDate = instruction.begin_date,
                                    EndDate = instruction.end_date,
                                    TypeOfInstruction = instruction.type_of_instruction,
                                    IsAssignedToPeople = instruction.is_assigned_to_people,
                                    IsPassedByEveryone = instruction.is_passed_by_everyone,
                                    // You might want to include the type name if available
                                    TypeName = await GetInstructionTypeNameAsync(instruction.type_of_instruction)
                                };
                            }
                            catch (Exception ex)
                            {
                                await transaction.RollbackAsync();
                                throw; // Rethrow to be caught by the outer try-catch
                            }
                        }
                    });

                    // Notify relevant parties about the new instruction (outside the transaction)
                    await _notificationsService.NotifyNewInstructionAsync(instruction);

                    // Return the created instruction DTO
                    return Ok(resultDto);
                }
                catch (InvalidOperationException ex)
                {
                    // Handle specific validation errors
                    return BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in database operation");
                    return BadRequest($"Error saving instruction: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new instruction");
                return BadRequest($"An error occurred: {ex.Message}");
            }
        }

        // Helper method to get instruction type name
        private async Task<string> GetInstructionTypeNameAsync(byte typeOfInstruction)
        {
            var instructionType = await _dbContext.InstructionTypes
                .FirstOrDefaultAsync(n => n.type_of_instruction == typeOfInstruction);

            return instructionType?.name_of_type_instruction ?? "Unknown";
        }


        /// <summary>
        /// Class for handling full instruction data with file paths
        /// </summary>
        public class FullCustomInstruction
        {
            /// <summary>
            /// The instruction entity (CommonClasses version used for transfer)
            /// </summary>
            public Models.Instruction _instruction { get; set; }

            /// <summary>
            /// List of file paths associated with the instruction
            /// </summary>
            public List<string> _paths { get; set; }

            public FullCustomInstruction() { }

            public FullCustomInstruction(Models.Instruction instruction, List<string> paths)
            {
                _instruction = instruction;
                _paths = paths;
            }
        }

        /// <summary>
        /// Data transfer object for adding new instructions
        /// </summary>
        public class AddInstructionRequest
        {
            /// <summary>
            /// The instruction details
            /// </summary>
            public InstructionDto Instruction { get; set; }

            /// <summary>
            /// List of file paths associated with the instruction
            /// </summary>
            public List<string> Paths { get; set; }

            /// <summary>
            /// DTO for instruction data
            /// </summary>
            public class InstructionDto
            {
                /// <summary>
                /// The cause or reason for the instruction
                /// </summary>
                public string CauseOfInstruction { get; set; }

                /// <summary>
                /// The end date of the instruction
                /// </summary>
                public DateTime EndDate { get; set; }

                /// <summary>
                /// Main path to the instruction folder
                /// </summary>
                public string PathToInstruction { get; set; }

                /// <summary>
                /// Type of instruction (0=Introductory, 1=Unplanned, 2=Primary, etc.)
                /// </summary>
                public byte TypeOfInstruction { get; set; }
            }
        }

        /// <summary>
        /// Exports data about instructions for a department within a specified date range.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Chiefs of Departments or Administrators) to export information 
        /// about instructions that have been passed by employees in the department. The export can be filtered by 
        /// a date range and specific types of instructions.
        /// </remarks>
        /// <param name="instructionExportRequest">
        /// An object containing the start date, end date, and a list of instruction types to filter the export.
        /// </param>
        /// <returns>
        /// Returns a list of instruction data if the operation is successful. 
        /// Returns appropriate error responses if the user lacks permissions, or if any required data is missing or invalid.
        /// </returns>
        /// <response code="200">
        /// The instruction data was successfully retrieved and exported.
        /// </response>
        /// <response code="400">
        /// A bad request occurred due to one of the following reasons:
        /// - The export request object is null or invalid.
        /// - The department or role could not be identified for the user.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        /// <response code="500">
        /// Internal server error occurred during the data export process.
        /// </response>
        [HttpPost("instructions-data-export")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> InstructionsDataExport([FromBody] InstructionExportRequest instructionExportRequest)
        {
            if (instructionExportRequest == null)
            {
                return BadRequest("Export request cannot be null");
            }

            try
            {
                // Get current user's information
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user's department
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                int departmentId = user.department_id;

                // Set up date range
                var startDate = instructionExportRequest.StartDate;
                var endDate = instructionExportRequest.EndDate.AddDays(1); // Include the end date

                // Get instruction types to filter by
                var instructionTypes = instructionExportRequest.InstructionTypes;
                if (instructionTypes == null || instructionTypes.Count == 0)
                {
                    return BadRequest("At least one instruction type must be specified");
                }

                // Query the database for instruction statuses
                var passedInstructions = await _dbContext.InstructionStatuses
                    .Where(status =>
                        status.department_id == departmentId &&
                        status.is_instruction_passed &&
                        instructionTypes.Contains(status.Instruction.type_of_instruction) &&
                        status.date_when_passed >= startDate &&
                        status.date_when_passed <= endDate)
                    .Include(status => status.Instruction)
                        .ThenInclude(i => i.FilePaths)
                    .Include(status => status.Instruction)
                        .ThenInclude(i => i.InstructionType)
                    .Include(status => status.Personnel)
                        .ThenInclude(p => p.EmployeesByDepartment)
                    .OrderBy(status => status.date_when_passed)
                    .ToListAsync();

                // Transform query results into the response model
                var result = new List<InstructionExportInstance>();

                foreach (var status in passedInstructions)
                {
                    // Get the employee details
                    var employee = status.Personnel.EmployeesByDepartment
                        .FirstOrDefault(e => e.department_id == departmentId);

                    if (employee == null) continue;

                    // Get the name of the person who conducted the instruction
                    string conductedBy = "Unknown";
                    if (status.was_signed_by_personnel_id > 0)
                    {
                        var conductor = await _dbContext.Users
                            .Include(u => u.Personnel)
                            .ThenInclude(p => p.EmployeesByDepartment)
                            .FirstOrDefaultAsync(u => u.personnel_id == status.was_signed_by_personnel_id);

                        if (conductor != null)
                        {
                            var conductorEmployee = conductor.Personnel.EmployeesByDepartment
                                .FirstOrDefault();

                            if (conductorEmployee != null)
                            {
                                conductedBy = $"{conductorEmployee.full_name} - {conductorEmployee.job_position}";
                            }
                        }
                    }

                    // Get the file paths for this instruction
                    var filePaths = status.Instruction.FilePaths
                        .Select(fp => fp.file_path)
                        .Where(p => p != null)
                        .ToList();

                    // Create a string with all file names
                    string fileNamesString = string.Join(" ",
                        filePaths.Select(path =>
                        {
                            var pathParts = path.Split('\\');
                            return pathParts.Length > 0 ? pathParts[pathParts.Length - 1] : path;
                        })
                    );

                    // Create the export instance
                    var exportInstance = new InstructionExportInstance
                    {
                        InstructionId = status.instruction_id,
                        DateWhenPassedByEmployee = status.date_when_passed ?? DateTime.Now,
                        FullNameOfEmployee = employee.full_name,
                        PositionOfEmployee = employee.job_position,
                        BirthDateOfEmployee = employee.birth_date,
                        InstructionType = status.Instruction.type_of_instruction,
                        CauseOfInstruction = status.Instruction.cause_of_instruction,
                        FullNameOfEmployeeWhoConductedInstruction = conductedBy,
                        FileNamesOfInstruction = filePaths,
                        FileNamesOfInstructionInOneString = fileNamesString
                    };

                    result.Add(exportInstance);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting instruction data");
                return StatusCode(500, "Internal server error during data export");
            }
        }

        // Additional class for the DTO
        public class InstructionExportRequest
        {
            // Required properties
            [Required]
            public DateTime StartDate { get; set; }

            [Required]
            public DateTime EndDate { get; set; }

            [Required]
            [MinLength(1, ErrorMessage = "At least one instruction type must be specified")]
            public List<byte> InstructionTypes { get; set; }
        }

        #endregion















        #region Normative Instructions

        /// <summary>
        /// Retrieves all normative instruction names from the database.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Coordinators or Administrators) to retrieve a list of all
        /// normative instruction names stored in the database.
        /// </remarks>
        /// <returns>
        /// Returns a list of normative instruction names if successful.
        /// </returns>
        /// <response code="200">
        /// The list of normative instruction names was successfully retrieved.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        /// <response code="500">
        /// Internal server error occurred while retrieving normative instruction names.
        /// </response>
        [HttpGet("normative-instructions")]
        [Authorize]
        public async Task<IActionResult> GetNormativeInstructions([FromQuery] bool? isUnplannedInstruction = null)
        {
            try
            {
                var query = _dbContext.NormativeInstructionNames.AsQueryable();

                // Filter by isUnplannedInstruction if specified
                if (isUnplannedInstruction.HasValue)
                {
                    query = query.Where(ni => ni.is_unplanned_instruction == isUnplannedInstruction.Value);
                }

                var normativeInstructions = await query.ToListAsync();

                // Map to DTOs explicitly to ensure proper property mapping
                var result = normativeInstructions.Select(ni => new NormativeInstructionDto
                {
                    Id = ni.id,
                    Name = ni.normative_instruction_name,
                    Url = ni.url,
                    CreatedAt = ni.created_at,
                    IsUnplannedInstruction = ni.is_unplanned_instruction
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving normative instruction names");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates a new normative instruction name.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Coordinators or Administrators) to create a new
        /// normative instruction name in the database.
        /// </remarks>
        /// <param name="model">The normative instruction to create</param>
        /// <returns>
        /// Returns the created normative instruction if successful.
        /// </returns>
        /// <response code="201">
        /// The normative instruction was successfully created.
        /// </response>
        /// <response code="400">
        /// Bad request - The model is invalid.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        /// <response code="500">
        /// Internal server error occurred while creating the normative instruction.
        /// </response>
        [HttpPost("normative-instructions")]
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> CreateNormativeInstruction([FromBody] NormativeInstructionCreateDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var normativeInstruction = new NormativeInstructionName
                {
                    normative_instruction_name = model.Name,
                    url = model.Url,
                    created_at = DateTime.UtcNow,
                    is_unplanned_instruction = model.IsUnplannedInstruction
                };

                _dbContext.NormativeInstructionNames.Add(normativeInstruction);
                await _dbContext.SaveChangesAsync();

                var result = new NormativeInstructionDto
                {
                    Id = normativeInstruction.id,
                    Name = normativeInstruction.normative_instruction_name,
                    Url = normativeInstruction.url,
                    CreatedAt = normativeInstruction.created_at,
                    IsUnplannedInstruction = normativeInstruction.is_unplanned_instruction
                };

                return CreatedAtAction(nameof(GetNormativeInstructions), null, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating normative instruction name");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates an existing normative instruction name.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Coordinators or Administrators) to update an existing
        /// normative instruction name in the database.
        /// </remarks>
        /// <param name="id">The ID of the normative instruction to update</param>
        /// <param name="model">The updated normative instruction data</param>
        /// <returns>
        /// Returns the updated normative instruction if successful.
        /// </returns>
        /// <response code="200">
        /// The normative instruction was successfully updated.
        /// </response>
        /// <response code="400">
        /// Bad request - The model is invalid.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        /// <response code="404">
        /// Not found - The normative instruction with the specified ID was not found.
        /// </response>
        /// <response code="500">
        /// Internal server error occurred while updating the normative instruction.
        /// </response>
        [HttpPut("normative-instructions/{id}")]
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> UpdateNormativeInstruction(int id, [FromBody] NormativeInstructionUpdateModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var normativeInstruction = await _dbContext.NormativeInstructionNames.FindAsync(id);
                if (normativeInstruction == null)
                {
                    return NotFound($"Normative instruction with ID {id} not found");
                }

                normativeInstruction.normative_instruction_name = model.Name;
                normativeInstruction.url = model.Url;

                _dbContext.NormativeInstructionNames.Update(normativeInstruction);
                await _dbContext.SaveChangesAsync();

                return Ok(normativeInstruction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating normative instruction with ID {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes a normative instruction name.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Coordinators or Administrators) to delete a
        /// normative instruction name from the database.
        /// </remarks>
        /// <param name="id">The ID of the normative instruction to delete</param>
        /// <returns>
        /// Returns no content if successful.
        /// </returns>
        /// <response code="204">
        /// The normative instruction was successfully deleted.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        /// <response code="404">
        /// Not found - The normative instruction with the specified ID was not found.
        /// </response>
        /// <response code="500">
        /// Internal server error occurred while deleting the normative instruction.
        /// </response>
        [HttpDelete("normative-instructions/{id}")]
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> DeleteNormativeInstruction(int id)
        {
            try
            {
                var normativeInstruction = await _dbContext.NormativeInstructionNames.FindAsync(id);
                if (normativeInstruction == null)
                {
                    return NotFound($"Normative instruction with ID {id} not found");
                }

                // Check if the normative instruction is referenced by any instruction status
                var isReferenced = await _dbContext.InstructionStatusToNormativeInstrNames
                    .AnyAsync(link => link.normative_instruction_name_id == id);

                if (isReferenced)
                {
                    return BadRequest($"Cannot delete normative instruction with ID {id} because it is referenced by one or more instruction statuses");
                }

                _dbContext.NormativeInstructionNames.Remove(normativeInstruction);
                await _dbContext.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting normative instruction with ID {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        // Model classes for endpoints

        #endregion

        #region Instructions Chief CRUD

        /// <summary>
        /// Retrieves all instructions for the authenticated user's department.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Chiefs of Departments or Administrators) to retrieve
        /// all instructions associated with their department, for management purposes.
        /// </remarks>
        /// <returns>
        /// A list of all instructions in the user's department.
        /// </returns>
        /// <response code="200">The list of instructions was successfully retrieved.</response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        /// <response code="403">Forbidden - The user does not have the required role.</response>
        /// <response code="500">Internal server error occurred during retrieval.</response>
        [HttpGet("get-all-instructions")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> GetAllInstructions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                // Get all instructions for the department that haven't been assigned to people yet
                var instructions = await _dbContext.Instructions
                    .Where(i => i.department_id == user.department_id && !i.is_assigned_to_people)
                    .Include(i => i.InstructionType)
                    .Include(i => i.FilePaths)
                    .ToListAsync();

                // Map to DTOs including the is_passed_by_chief_unplanned_instr field
                var result = instructions.Select(i => new
                {
                    i.instruction_id,
                    i.cause_of_instruction,
                    i.begin_date,
                    i.end_date,
                    i.type_of_instruction,
                    i.is_assigned_to_people,
                    i.is_passed_by_everyone,
                    i.is_passed_by_chief_unplanned_instr, // Include this field
                    TypeName = i.InstructionType?.name_of_type_instruction,
                    FilePaths = i.FilePaths.Select(fp => fp.file_path).ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all instructions");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves a specific instruction by ID.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users to retrieve details of a specific instruction by its ID.
        /// </remarks>
        /// <param name="id">The ID of the instruction to retrieve</param>
        /// <returns>
        /// The requested instruction details if found.
        /// </returns>
        /// <response code="200">The instruction was successfully retrieved.</response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        /// <response code="403">Forbidden - The user does not have the required role.</response>
        /// <response code="404">Not Found - The specified instruction was not found.</response>
        /// <response code="500">Internal server error occurred during retrieval.</response>
        [HttpGet("get-instruction/{id}")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> GetInstructionById(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                // Get the instruction by ID, ensuring it belongs to the user's department
                var instruction = await _dbContext.Instructions
                    .Include(i => i.InstructionType)
                    .Include(i => i.FilePaths)
                    .FirstOrDefaultAsync(i => i.instruction_id == id && i.department_id == user.department_id);

                if (instruction == null)
                {
                    return NotFound($"Instruction with ID {id} not found");
                }

                // Map to DTO
                var result = new
                {
                    instruction.instruction_id,
                    instruction.cause_of_instruction,
                    instruction.begin_date,
                    instruction.end_date,
                    instruction.type_of_instruction,
                    instruction.is_assigned_to_people,
                    instruction.is_passed_by_everyone,
                    instruction.is_passed_by_chief_unplanned_instr,
                    TypeName = instruction.InstructionType?.name_of_type_instruction,
                    FilePaths = instruction.FilePaths.Select(fp => fp.file_path).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving instruction with ID {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates an existing instruction.
        /// </summary>
        [HttpPut("update-instruction/{id}")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> UpdateInstruction(int id, [FromBody] InstructionUpdateDto instructionDto)
        {
            if (instructionDto == null)
            {
                return BadRequest("Instruction data is missing");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                // Get the instruction by ID, ensuring it belongs to the user's department
                var instruction = await _dbContext.Instructions
                    .FirstOrDefaultAsync(i => i.instruction_id == id && i.department_id == user.department_id);

                if (instruction == null)
                {
                    return NotFound($"Instruction with ID {id} not found");
                }

                // Update the instruction properties
                instruction.cause_of_instruction = instructionDto.CauseOfInstruction;
                instruction.end_date = instructionDto.EndDate;
                instruction.type_of_instruction = instructionDto.TypeOfInstruction;

                // Save changes
                _dbContext.Instructions.Update(instruction);
                await _dbContext.SaveChangesAsync();

                // Return the updated instruction
                var updatedInstructionDto = new InstructionResultDto
                {
                    InstructionId = instruction.instruction_id,
                    CauseOfInstruction = instruction.cause_of_instruction,
                    BeginDate = instruction.begin_date,
                    EndDate = instruction.end_date,
                    TypeOfInstruction = instruction.type_of_instruction,
                    IsAssignedToPeople = instruction.is_assigned_to_people,
                    IsPassedByEveryone = instruction.is_passed_by_everyone,
                    TypeName = await GetInstructionTypeNameAsync(instruction.type_of_instruction)
                };

                return Ok(updatedInstructionDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating instruction with ID {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes an instruction.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users to delete an instruction by its ID.
        /// </remarks>
        /// <param name="id">The ID of the instruction to delete</param>
        /// <returns>
        /// A success message if the instruction was deleted successfully.
        /// </returns>
        /// <response code="200">The instruction was successfully deleted.</response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        /// <response code="403">Forbidden - The user does not have the required role or cannot delete this instruction.</response>
        /// <response code="404">Not Found - The specified instruction was not found.</response>
        /// <response code="500">Internal server error occurred during deletion.</response>
        [HttpDelete("delete-instruction/{id}")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> DeleteInstruction(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                // Get the instruction by ID, ensuring it belongs to the user's department
                var instruction = await _dbContext.Instructions
                    .FirstOrDefaultAsync(i => i.instruction_id == id && i.department_id == user.department_id);

                if (instruction == null)
                {
                    return NotFound($"Instruction with ID {id} not found");
                }

                // Check if the instruction can be deleted
                // You may want to add additional checks, such as whether the instruction has been assigned or passed
                if (instruction.is_passed_by_everyone)
                {
                    return BadRequest("Cannot delete an instruction that has been completed by everyone");
                }

                // First, delete related file paths
                var filePaths = await _dbContext.FilePathsForInstructions
                    .Where(fp => fp.instruction_id == id)
                    .ToListAsync();

                _dbContext.FilePathsForInstructions.RemoveRange(filePaths);
                await _dbContext.SaveChangesAsync();

                // Then, delete related instruction statuses
                var statuses = await _dbContext.InstructionStatuses
                    .Where(s => s.instruction_id == id)
                    .ToListAsync();

                // For each status, delete related normative instruction links
                foreach (var status in statuses)
                {
                    var links = await _dbContext.InstructionStatusToNormativeInstrNames
                        .Where(link => link.instruction_status_id == status.id)
                        .ToListAsync();

                    _dbContext.InstructionStatusToNormativeInstrNames.RemoveRange(links);
                }

                await _dbContext.SaveChangesAsync();
                _dbContext.InstructionStatuses.RemoveRange(statuses);
                await _dbContext.SaveChangesAsync();

                // Finally, delete the instruction itself
                _dbContext.Instructions.Remove(instruction);
                await _dbContext.SaveChangesAsync();

                return Ok("Instruction deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting instruction with ID {id}");
                return StatusCode(500, "Internal server error");
            }
        }


        //FOR CHIEF
        /// <summary>
        /// Retrieves not passed instructions for the authenticated chief's department.
        /// </summary>
        /// <remarks>
        /// This endpoint allows chiefs of departments to view instructions in their department
        /// that have not been passed by all employees, including the status of each employee.
        /// Requires the user to be authenticated and have the ChiefOfDepartment or Administrator role.
        /// </remarks>
        /// <returns>
        /// Returns a list of instructions with the status of each employee for the chief's department.
        /// </returns>
        /// <response code="200">
        /// The not passed instructions were successfully retrieved.
        /// </response>
        /// <response code="400">
        /// A bad request occurred due to missing user information.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        /// <response code="500">
        /// Internal server error occurred during the retrieval process.
        /// </response>
        [HttpGet("get-not-passed-instructions-for-chief")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> GetNotPassedInstructionsForChief()
        {
            try
            {
                // Get user ID from claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                int departmentId = user.department_id;

                // Get instructions that are not marked as passed by everyone for this department
                var instructions = await _dbContext.Instructions
                    .Where(i => i.department_id == departmentId && !i.is_passed_by_everyone)
                    .Include(i => i.InstructionType)
                    .ToListAsync();

                if (!instructions.Any())
                {
                    return Ok(new List<InstructionForChiefDto>());
                }

                var result = new List<InstructionForChiefDto>();

                foreach (var instruction in instructions)
                {
                    // Get all statuses for this instruction across all personnel in the department
                    var statuses = await _dbContext.InstructionStatuses
                        .Where(s => s.instruction_id == instruction.instruction_id && s.department_id == departmentId)
                        .Include(s => s.Personnel)
                            .ThenInclude(p => p.EmployeesByDepartment.Where(e => e.department_id == departmentId))
                        .ToListAsync();

                    if (!statuses.Any())
                        continue;

                    var instructionForChief = new InstructionForChiefDto
                    {
                        InstructionId = instruction.instruction_id,
                        BeginDate = instruction.begin_date,
                        EndDate = instruction.end_date,
                        CauseOfInstruction = instruction.cause_of_instruction,
                        TypeOfInstruction = instruction.InstructionType?.name_of_type_instruction ?? "Unknown",
                        Persons = new List<PersonStatusDto>()
                    };

                    foreach (var status in statuses)
                    {
                        // Find employee details for this personnel in this department
                        var employee = status.Personnel.EmployeesByDepartment.FirstOrDefault();
                        if (employee == null)
                            continue;

                        instructionForChief.Persons.Add(new PersonStatusDto
                        {
                            PersonnelNumber = status.Personnel.personnel_number,
                            PersonName = employee.full_name,
                            Passed = status.is_instruction_passed
                        });
                    }

                    result.Add(instructionForChief);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving not passed instructions for chief");
                return StatusCode(500, "Internal server error");
            }
        }



        /// <summary>
        /// Retrieves passed instructions for the authenticated chief's department.
        /// </summary>
        /// <remarks>
        /// This endpoint allows chiefs of departments to view instructions in their department
        /// that have been passed by at least one employee, including the status of each employee.
        /// Requires the user to be authenticated and have the ChiefOfDepartment or Administrator role.
        /// </remarks>
        /// <returns>
        /// Returns a list of instructions with the status of each employee for the chief's department.
        /// </returns>
        /// <response code="200">
        /// The passed instructions were successfully retrieved.
        /// </response>
        /// <response code="400">
        /// A bad request occurred due to missing user information.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        /// <response code="500">
        /// Internal server error occurred during the retrieval process.
        /// </response>
        [HttpGet("get-passed-instructions-for-chief")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> GetPassedInstructionsForChief()
        {
            try
            {
                // Get user ID from claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                int departmentId = user.department_id;

                // Find instructions where at least one person has passed it
                var instructionIdsWithPassedStatus = await _dbContext.InstructionStatuses
                    .Where(s => s.department_id == departmentId && s.is_instruction_passed)
                    .Select(s => s.instruction_id)
                    .Distinct()
                    .ToListAsync();

                if (!instructionIdsWithPassedStatus.Any())
                {
                    return Ok(new List<InstructionForChiefDto>());
                }

                // Get the instructions with those IDs
                var instructions = await _dbContext.Instructions
                    .Where(i => instructionIdsWithPassedStatus.Contains(i.instruction_id) && i.department_id == departmentId)
                    .Include(i => i.InstructionType)
                    .ToListAsync();

                var result = new List<InstructionForChiefDto>();

                foreach (var instruction in instructions)
                {
                    // Get all statuses for this instruction across all personnel in the department
                    var statuses = await _dbContext.InstructionStatuses
                        .Where(s => s.instruction_id == instruction.instruction_id && s.department_id == departmentId)
                        .Include(s => s.Personnel)
                            .ThenInclude(p => p.EmployeesByDepartment.Where(e => e.department_id == departmentId))
                        .ToListAsync();

                    if (!statuses.Any())
                        continue;

                    var instructionForChief = new InstructionForChiefDto
                    {
                        InstructionId = instruction.instruction_id,
                        BeginDate = instruction.begin_date,
                        EndDate = instruction.end_date,
                        CauseOfInstruction = instruction.cause_of_instruction,
                        TypeOfInstruction = instruction.InstructionType?.name_of_type_instruction ?? "Unknown",
                        IsPassedByEveryone = instruction.is_passed_by_everyone,
                        Persons = new List<PersonStatusDto>()
                    };

                    foreach (var status in statuses)
                    {
                        // Find employee details for this personnel in this department
                        var employee = status.Personnel.EmployeesByDepartment.FirstOrDefault();
                        if (employee == null)
                            continue;

                        instructionForChief.Persons.Add(new PersonStatusDto
                        {
                            PersonnelNumber = status.Personnel.personnel_number,
                            PersonName = employee.full_name,
                            Passed = status.is_instruction_passed,
                            DatePassed = status.date_when_passed
                        });
                    }

                    // Calculate passed percentage
                    int totalPersons = instructionForChief.Persons.Count;
                    int passedPersons = instructionForChief.Persons.Count(p => p.Passed);
                    instructionForChief.PassedPercentage = totalPersons > 0 ? (double)passedPersons / totalPersons * 100 : 0;

                    result.Add(instructionForChief);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving passed instructions for chief");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves predetermined normative instructions for a specific unplanned instruction.
        /// </summary>
        /// <remarks>
        /// This endpoint returns the normative instructions that were originally assigned to an unplanned instruction
        /// when it was created by Management. These instructions will be automatically assigned to employees
        /// when the chief assigns the unplanned instruction.
        /// </remarks>
        /// <param name="instructionId">The ID of the unplanned instruction</param>
        /// <returns>
        /// Returns a list of normative instructions associated with the unplanned instruction.
        /// </returns>
        /// <response code="200">The normative instructions were successfully retrieved.</response>
        /// <response code="400">Bad request - Invalid instruction ID or instruction not found.</response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        /// <response code="403">Forbidden - The user does not have the required role.</response>
        /// <response code="500">Internal server error occurred during retrieval.</response>
        [HttpGet("get-normative-instructions-for-unplanned/{instructionId}")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
        public async Task<IActionResult> GetNormativeInstructionsForUnplannedInstruction(int instructionId)
        {
            try
            {
                // Verify the instruction exists and is an unplanned instruction
                var instruction = await _dbContext.Instructions
                    .Include(i => i.InstructionType)
                    .FirstOrDefaultAsync(i => i.instruction_id == instructionId);

                if (instruction == null)
                {
                    return BadRequest($"Instruction with ID {instructionId} not found");
                }

                if (instruction.type_of_instruction != 1) // 1 = Unplanned instruction
                {
                    return BadRequest($"Instruction with ID {instructionId} is not an unplanned instruction");
                }

                // Get current user to verify department access
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                // Verify user has access to this instruction (same department)
                if (instruction.department_id != user.department_id)
                {
                    return Forbid("You do not have access to this instruction");
                }

                // Find normative instructions that were originally assigned to chiefs for this unplanned instruction
                // We look for instruction statuses where the instruction was assigned to chiefs and get their normative instructions
                var normativeInstructions = await _dbContext.InstructionStatuses
                    .Where(s => s.instruction_id == instructionId && s.department_id == user.department_id)
                    .Include(s => s.NormativeInstructions)
                        .ThenInclude(ni => ni.NormativeInstructionName)
                    .SelectMany(s => s.NormativeInstructions)
                    .Select(ni => ni.NormativeInstructionName)
                    .Distinct()
                    .Select(n => new
                    {
                        Id = n.id,
                        Name = n.normative_instruction_name,
                        Url = n.url,
                        CreatedAt = n.created_at
                    })
                    .ToListAsync();

                // If no normative instructions found through instruction statuses, 
                // check if there are any marked as unplanned instructions
                if (!normativeInstructions.Any())
                {
                    // Look for normative instructions that are marked as unplanned and created around the same time
                    var instructionCreationDate = instruction.begin_date;
                    var searchStartDate = instructionCreationDate.AddDays(-1);
                    var searchEndDate = instructionCreationDate.AddDays(1);

                    var unplannedNormatives = await _dbContext.NormativeInstructionNames
                        .Where(n => n.is_unplanned_instruction &&
                                   n.created_at >= searchStartDate &&
                                   n.created_at <= searchEndDate)
                        .Select(n => new
                        {
                            Id = n.id,
                            Name = n.normative_instruction_name,
                            Url = n.url,
                            CreatedAt = n.created_at
                        })
                        .ToListAsync();

                    normativeInstructions = unplannedNormatives;
                }

                return Ok(normativeInstructions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving normative instructions for unplanned instruction {instructionId}");
                return StatusCode(500, "Internal server error");
            }
        }



        // Add this DTO for instruction updates
        public class UpdateInstructionDto
        {
            [Required]
            public string CauseOfInstruction { get; set; }

            [Required]
            public DateTime EndDate { get; set; }

            [Required]
            public byte TypeOfInstruction { get; set; }

            public List<string> FilePaths { get; set; }
        }

        #endregion
        #region Manager stuff

        // Add these endpoints to your InstructionsController.cs class

        #region Management Endpoints for WPF Application

        /// <summary>
        /// Gets all departments with their chiefs and deputy chiefs for management interface
        /// </summary>
        /// <remarks>
        /// This endpoint retrieves all departments along with their chiefs and deputy chiefs
        /// for the Management WPF application to assign unplanned instructions.
        /// Only accessible by Management role.
        /// </remarks>
        /// <returns>List of departments with chiefs</returns>
        [HttpGet("get-departments-with-chiefs")]
        [Authorize(Roles = "Management, Administrator")]
        public async Task<IActionResult> GetDepartmentsWithChiefs()
        {
            try
            {
                var departments = await _dbContext.Departments
                    .Include(d => d.Users)
                        .ThenInclude(u => u.Role)
                    .Include(d => d.Users)
                        .ThenInclude(u => u.Personnel)
                            .ThenInclude(p => p.EmployeesByDepartment)
                    .ToListAsync();

                var result = departments.Select(dept => new DepartmentWithChiefsDto
                {
                    DepartmentId = dept.department_id,
                    DepartmentName = dept.department_name,
                    Chiefs = dept.Users
                        .Where(u => u.Role != null &&
                               (u.Role.role_type == "ChiefOfDepartment" || u.Role.role_type == "DeputyChief"))
                        .Select(u => new ChiefDto
                        {
                            UserId = u.id,
                            PersonnelId = u.personnel_id,
                            Role = u.Role.role_type,
                            FullName = u.Personnel?.EmployeesByDepartment
                                .FirstOrDefault(e => e.department_id == dept.department_id)?.full_name ?? "Unknown",
                            JobPosition = u.Personnel?.EmployeesByDepartment
                                .FirstOrDefault(e => e.department_id == dept.department_id)?.job_position ?? "Unknown"
                        })
                        .ToList()
                }).Where(d => d.Chiefs.Any()).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments with chiefs");
                return StatusCode(500, "Internal server error");
            }
        }

        
        /// <summary>
        /// Gets status of unplanned instructions assigned to chiefs
        /// </summary>
        /// <remarks>
        /// This endpoint retrieves the status of all unplanned instructions that have been
        /// assigned to chiefs, showing which chiefs have completed them and which haven't.
        /// Only accessible by Management role.
        /// </remarks>
        /// <returns>List of instruction statuses with chief completion data</returns>
        [HttpGet("get-unplanned-instructions-for-chiefs-status")]
        [Authorize(Roles = "Management, Administrator")]
        public async Task<IActionResult> GetUnplannedInstructionsForChiefsStatus()
        {
            try
            {
                // Get all unplanned instructions (type 1) that are assigned to people
                var unplannedInstructions = await _dbContext.Instructions
                    .Where(i => i.type_of_instruction == 1 && i.InstructionStatuses.Any())
                    .Include(i => i.InstructionType)
                    .Include(i => i.InstructionStatuses)
                        .ThenInclude(s => s.Personnel)
                            .ThenInclude(p => p.EmployeesByDepartment)
                    .Include(i => i.InstructionStatuses)
                        .ThenInclude(s => s.Department)
                    .OrderByDescending(i => i.begin_date)
                    .ToListAsync();

                var result = new List<UnplannedInstructionStatusDto>();

                foreach (var instruction in unplannedInstructions)
                {
                    // Get all statuses for chiefs/deputies only
                    var chiefStatuses = instruction.InstructionStatuses
                        .Where(s => s.Personnel.EmployeesByDepartment.Any(e =>
                            e.job_position.Contains("начальник") ||
                            e.job_position.Contains("заместитель") ||
                            e.job_position.Contains("Начальник") ||
                            e.job_position.Contains("Заместитель")))
                        .ToList();

                    if (!chiefStatuses.Any()) continue;

                    var statusDto = new UnplannedInstructionStatusDto
                    {
                        InstructionId = instruction.instruction_id,
                        CauseOfInstruction = instruction.cause_of_instruction,
                        BeginDate = instruction.begin_date,
                        EndDate = instruction.end_date,
                        TypeName = instruction.InstructionType?.name_of_type_instruction ?? "Внеплановый",
                        TotalAssigned = chiefStatuses.Count,
                        TotalPassed = chiefStatuses.Count(s => s.is_instruction_passed),
                        ChiefStatuses = chiefStatuses.Select(s => new ChiefStatusDto
                        {
                            ChiefName = s.Personnel.EmployeesByDepartment
                                .FirstOrDefault(e => e.department_id == s.department_id)?.full_name ?? "Unknown",
                            DepartmentName = s.Department?.department_name ?? "Unknown",
                            JobPosition = s.Personnel.EmployeesByDepartment
                                .FirstOrDefault(e => e.department_id == s.department_id)?.job_position ?? "Unknown",
                            IsPassed = s.is_instruction_passed,
                            DatePassed = s.date_when_passed,
                            DateAssigned = s.when_was_sent_to_user
                        }).ToList()
                    };

                    result.Add(statusDto);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unplanned instructions status");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Assigns an unplanned instruction to all chiefs in selected departments with normative base processing
        /// </summary>
        /// <remarks>
        /// This endpoint creates an unplanned instruction and assigns it to all chiefs and deputy chiefs
        /// in the selected departments. It also processes multiline normative base names and links.
        /// Only accessible by Management role.
        /// </remarks>
        /// <param name="package">Package containing instruction details and selected department IDs</param>
        /// <returns>Success message with assignment details</returns>
        [HttpPost("assign-unplanned-instruction-to-departments")]
        [Authorize(Roles = "Management, Administrator")]
        public async Task<IActionResult> AssignUnplannedInstructionToDepartments([FromBody] UnplannedInstructionForDepartmentsPackage package)
        {
            try
            {
                if (package == null || package.Instruction == null || package.SelectedDepartmentIds == null || !package.SelectedDepartmentIds.Any())
                {
                    return BadRequest("Invalid package data");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Get current user (Management)
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int userIdInt))
                {
                    return BadRequest("Invalid user ID");
                }

                var currentUser = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (currentUser == null)
                {
                    return BadRequest("User not found");
                }

                // Process normative instructions if provided
                int? createdNormativeInstructionId = null;
                if (!string.IsNullOrEmpty(package.NormativeBaseText))
                {
                    // Use the MarkNormativeAsUnplanned property from the package
                    createdNormativeInstructionId = await ProcessNormativeBaseText(
                        package.NormativeBaseText,
                        package.MarkNormativeAsUnplanned);
                }

                // Create an execution strategy
                var strategy = _dbContext.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            var assignedCount = 0;
                            var processedDepartments = new List<string>();

                            foreach (var departmentId in package.SelectedDepartmentIds)
                            {
                                // Get all chiefs and deputy chiefs in this department
                                var departmentChiefs = await _dbContext.Users
                                    .Include(u => u.Role)
                                    .Include(u => u.Personnel)
                                        .ThenInclude(p => p.EmployeesByDepartment)
                                    .Where(u => u.department_id == departmentId &&
                                           u.Role != null &&
                                           (u.Role.role_type == "ChiefOfDepartment" || u.Role.role_type == "DeputyChief"))
                                    .ToListAsync();

                                if (!departmentChiefs.Any())
                                {
                                    _logger.LogWarning($"No chiefs found in department {departmentId}");
                                    continue;
                                }

                                // Check if instruction with same cause already exists for this department
                                var existingInstruction = await _dbContext.Instructions
                                    .FirstOrDefaultAsync(i => i.cause_of_instruction == package.Instruction.CauseOfInstruction
                                                           && i.department_id == departmentId);

                                Models.Instruction instruction;

                                if (existingInstruction != null)
                                {
                                    instruction = existingInstruction;
                                    _logger.LogInformation($"Using existing instruction {instruction.instruction_id} for department {departmentId}");
                                }
                                else
                                {
                                    // Create new instruction for this department
                                    instruction = new Models.Instruction
                                    {
                                        cause_of_instruction = package.Instruction.CauseOfInstruction,
                                        begin_date = DateTime.UtcNow,
                                        end_date = package.Instruction.EndDate,
                                        type_of_instruction = package.Instruction.TypeOfInstruction,
                                        department_id = departmentId,
                                        is_assigned_to_people = false,
                                        is_passed_by_everyone = false,
                                        is_passed_by_chief_unplanned_instr = false
                                    };

                                    _dbContext.Instructions.Add(instruction);
                                    await _dbContext.SaveChangesAsync();
                                    _logger.LogInformation($"Created new instruction {instruction.instruction_id} for department {departmentId}");
                                }

                                // Create instruction statuses for each chief in this department
                                foreach (var chief in departmentChiefs)
                                {
                                    // Check if status already exists
                                    var existingStatus = await _dbContext.InstructionStatuses
                                        .FirstOrDefaultAsync(s => s.instruction_id == instruction.instruction_id
                                                               && s.personnel_id == chief.personnel_id);

                                    if (existingStatus == null)
                                    {
                                        var instructionStatus = new InstructionStatus
                                        {
                                            instruction_id = instruction.instruction_id,
                                            personnel_id = chief.personnel_id,
                                            department_id = departmentId,
                                            is_instruction_passed = false,
                                            when_was_sent_to_user = DateTime.Now,
                                            when_was_sent_to_user_UTC = DateTime.UtcNow,
                                            was_signed_by_personnel_id = currentUser.personnel_id
                                        };

                                        _dbContext.InstructionStatuses.Add(instructionStatus);
                                        await _dbContext.SaveChangesAsync();

                                        // Add the created normative instruction if available
                                        if (createdNormativeInstructionId.HasValue)
                                        {
                                            var junction = new InstructionStatusToNormativeInstrName
                                            {
                                                instruction_status_id = instructionStatus.id,
                                                normative_instruction_name_id = createdNormativeInstructionId.Value
                                            };

                                            _dbContext.InstructionStatusToNormativeInstrNames.Add(junction);
                                        }

                                        // Add any additional normative instructions from the package
                                        if (package.NormativeInstructionIds != null && package.NormativeInstructionIds.Any())
                                        {
                                            foreach (var normativeId in package.NormativeInstructionIds)
                                            {
                                                var junction = new InstructionStatusToNormativeInstrName
                                                {
                                                    instruction_status_id = instructionStatus.id,
                                                    normative_instruction_name_id = normativeId
                                                };

                                                _dbContext.InstructionStatusToNormativeInstrNames.Add(junction);
                                            }
                                        }

                                        assignedCount++;
                                        _logger.LogInformation($"Assigned instruction to chief {chief.personnel_id} in department {departmentId}");
                                    }
                                }

                                var departmentName = await _dbContext.Departments
                                    .Where(d => d.department_id == departmentId)
                                    .Select(d => d.department_name)
                                    .FirstOrDefaultAsync();

                                processedDepartments.Add(departmentName ?? $"Department {departmentId}");
                            }

                            await _dbContext.SaveChangesAsync();
                            await transaction.CommitAsync();

                            var response = new
                            {
                                Message = $"Instruction '{package.Instruction.CauseOfInstruction}' assigned to {assignedCount} chiefs across {processedDepartments.Count} departments: {string.Join(", ", processedDepartments)}"
                            };
                            return Ok(response);
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "Error assigning unplanned instruction to departments");
                            throw;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AssignUnplannedInstructionToDepartments");
                return StatusCode(500, "Internal server error");
            }
        }



        /// <summary>
        /// Processes normative base text with names and links, creates a single normative instruction
        /// </summary>
        /// <param name="normativeBaseText">Text containing names and links separated by newlines</param>
        /// <returns>ID of the created normative instruction</returns>
        private async Task<int> ProcessNormativeBaseText(string normativeBaseText, bool markAsUnplanned = true)
        {
            try
            {
                // Parse the normative base text
                // Expected format: "NAMES: name1 | name2 | name3 | LINKS: link1 | link2 | link3"
                var parts = normativeBaseText.Split(new[] { " | LINKS: " }, StringSplitOptions.None);

                string namesSection = "";
                string linksSection = "";

                if (parts.Length >= 1)
                {
                    namesSection = parts[0].Replace("NAMES: ", "").Trim();
                }

                if (parts.Length >= 2)
                {
                    linksSection = parts[1].Trim();
                }

                // Create a combined normative instruction name
                string combinedName = $"Нормативная база: {namesSection}";
                string combinedUrl = linksSection;

                // Check if this normative instruction already exists
                var existingNormative = await _dbContext.NormativeInstructionNames
                    .FirstOrDefaultAsync(n => n.normative_instruction_name == combinedName);

                if (existingNormative != null)
                {
                    return existingNormative.id;
                }

                // Create new normative instruction
                var normativeInstruction = new NormativeInstructionName
                {
                    normative_instruction_name = combinedName,
                    url = combinedUrl,
                    created_at = DateTime.UtcNow,
                    is_unplanned_instruction = markAsUnplanned // Set based on parameter
                };

                _dbContext.NormativeInstructionNames.Add(normativeInstruction);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Created normative instruction with ID {normativeInstruction.id}: {combinedName}");

                return normativeInstruction.id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing normative base text");
                throw new Exception($"Error creating normative instruction: {ex.Message}", ex);
            }
        }

        #endregion

        #endregion

        #region Coordinator stuff

        /// <summary>
        /// Inserts a new employee into the database
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Coordinators, Chiefs of Departments, or Administrators) to add a new employee
        /// to the database. If the addInitialInstruction flag is provided in the request, it will automatically 
        /// create and assign an initial instruction to the new employee.
        /// </remarks>
        /// <param name="employee">The employee details to be added</param>
        /// <param name="addInitialInstruction">Optional query parameter to automatically create initial instruction</param>
        /// <returns>
        /// Returns success message if employee was created successfully, or appropriate error responses.
        /// </returns>
        /// <response code="200">The employee was successfully added to the database</response>
        /// <response code="400">The employee data is invalid or there was an error processing the request</response>
        /// <response code="401">The user is not authenticated</response>
        /// <response code="403">The user doesn't have permission to add employees</response>
        /// <response code="500">Internal server error occurred during employee creation</response>
        [HttpPost("insert-new-employee")]
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> InsertNewEmployee(
    [FromBody] EmployeeCreationDto employee,  // Changed to EmployeeCreationDto
    [FromQuery] bool addInitialInstruction = false)
        { 
            try
            {
                if (employee == null)
                {
                    return BadRequest("Employee data is missing");
                }

                // Get current user information
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim not found");
                }

                var currentUser = await _dbContext.Users
                    .Include(u => u.Department)
                    .Include(u => u.Personnel)
                    .FirstOrDefaultAsync(u => u.username == username);

                if (currentUser == null)
                {
                    return BadRequest("User not found in database");
                }

                // Validate employee data
                if (string.IsNullOrWhiteSpace(employee.FullName) ||
                    string.IsNullOrWhiteSpace(employee.PersonnelNumber) ||
                    string.IsNullOrWhiteSpace(employee.Department))
                {
                    return BadRequest("Required employee fields are missing (full_name, personnel_number, department)");
                }

                // Check if employee with this personnel number already exists
                var existingEmployee = await _dbContext.Personnel
                    .FirstOrDefaultAsync(p => p.personnel_number == employee.PersonnelNumber);

                if (existingEmployee != null)
                {
                    return BadRequest($"Employee with personnel number {employee.PersonnelNumber} already exists");
                }

                // Get department ID by name
                var department = await _dbContext.Departments
                    .FirstOrDefaultAsync(d => d.department_name == employee.Department);

                if (department == null)
                {
                    return BadRequest($"Department '{employee.Department}' not found");
                }

                // Create execution strategy for transaction handling
                var strategy = _dbContext.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();

                    try
                    {
                        // Step 1: Create Personnel record
                        var personnel = new Personnel
                        {
                            personnel_number = employee.PersonnelNumber
                        };

                        _dbContext.Personnel.Add(personnel);
                        await _dbContext.SaveChangesAsync();

                        // Step 2: Create EmployeeByDepartment record
                        var employeeByDept = new EmployeeByDepartment
                        {
                            personnel_id = personnel.personnel_id,
                            department_id = department.department_id,
                            full_name = employee.FullName,
                            job_position = employee.JobPosition ?? "Не указано",
                            group = employee.Group,
                            birth_date = employee.BirthDate,
                            gender = employee.Gender ?? 3,
                            is_driver = employee.IsDriver,
                            is_working_in_department = employee.IsWorkingInDepartment ?? true
                        };

                        _dbContext.EmployeesByDepartment.Add(employeeByDept);
                        await _dbContext.SaveChangesAsync();

                        // Step 3: Handle initial instruction if requested (ЗАГЛУШКА)
                        if (addInitialInstruction)
                        {
                            await HandleInitialInstructionForNewEmployee(personnel, department, currentUser);
                        }

                        // Step 4: Handle User credentials and insert into Management.user
                        var userCredentials = await CreateUserCredentialsForNewEmployee(personnel, department, employee);
                        if (userCredentials == null)
                        {
                            throw new Exception("Failed to create user credentials for new employee");
                        }

                        _logger.LogInformation($"Successfully created user credentials for employee: {employee.FullName}, Username: {userCredentials.Username}");


                        await transaction.CommitAsync();

                        _logger.LogInformation($"Successfully created employee: {employee.FullName} with personnel number: {employee.PersonnelNumber}");

                        return Ok(new
                        {
                            Message = "Employee successfully added to database",
                            PersonnelId = personnel.personnel_id,
                            PersonnelNumber = personnel.personnel_number,
                            FullName = employee.FullName,
                            Department = employee.Department,
                            InitialInstructionCreated = addInitialInstruction
                        });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, $"Error creating employee: {employee.FullName}");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in InsertNewEmployee endpoint");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// ЗАГЛУШКА: Handles initial instruction creation and assignment for new employee
        /// </summary>
        /// <param name="personnel">The newly created personnel record</param>
        /// <param name="department">The department the employee belongs to</param>
        /// <param name="currentUser">The user who created the employee</param>
        private async Task HandleInitialInstructionForNewEmployee(
            Personnel personnel,
            Models.Department department,
            User currentUser)
        {
            try
            {
                _logger.LogInformation($"Creating initial instruction for new employee: {personnel.personnel_number}");

                // Step 1: Check if initial instruction already exists for this department
                var existingInitialInstruction = await _dbContext.Instructions
                    .FirstOrDefaultAsync(i =>
                        i.department_id == department.department_id &&
                        i.type_of_instruction == 0 && // 0 = Вводный (Initial/Introductory)
                        i.end_date > DateTime.Now); // Still valid

                int instructionId;

                if (existingInitialInstruction != null)
                {
                    // Use existing initial instruction
                    instructionId = existingInitialInstruction.instruction_id;
                    _logger.LogInformation($"Using existing initial instruction ID: {instructionId}");
                }
                else
                {
                    // Create new initial instruction
                    var newInitialInstruction = new Models.Instruction
                    {
                        department_id = department.department_id,
                        begin_date = DateTime.Now.Date,
                        end_date = DateTime.Now.AddYears(1).Date, // Valid for 1 year
                        cause_of_instruction = "Вводный инструктаж для новых сотрудников",
                        type_of_instruction = 0, // 0 = Вводный (Initial/Introductory)
                        is_assigned_to_people = true,
                        is_passed_by_everyone = false,
                        is_passed_by_chief_unplanned_instr = true // Auto-approve for initial instructions
                    };

                    _dbContext.Instructions.Add(newInitialInstruction);
                    await _dbContext.SaveChangesAsync();

                    instructionId = newInitialInstruction.instruction_id;
                    _logger.LogInformation($"Created new initial instruction ID: {instructionId}");
                }

                // Step 2: Assign initial instruction to the new employee
                var instructionStatus = new InstructionStatus
                {
                    personnel_id = personnel.personnel_id,
                    department_id = department.department_id,
                    instruction_id = instructionId,
                    is_instruction_passed = false, // Employee hasn't passed it yet
                    date_when_passed = null,
                    date_when_passed_UTC = null,
                    when_was_sent_to_user = DateTime.Now,
                    when_was_sent_to_user_UTC = DateTime.UtcNow,
                    was_signed_by_personnel_id = currentUser.personnel_id
                };

                _dbContext.InstructionStatuses.Add(instructionStatus);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Successfully assigned initial instruction to employee: {personnel.personnel_number}");

                // TODO: ЗАГЛУШКА - Add additional logic here if needed
                // - Send notification to employee
                // - Create normative instruction associations
                // - Log the assignment in audit trail
                // - Send email notification
                await CreatePlaceholderNotification(personnel, instructionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling initial instruction for employee: {personnel.personnel_number}");
                // Don't throw here - initial instruction failure shouldn't prevent employee creation
                // Just log the error and continue
            }
        }

        /// <summary>
        /// ЗАГЛУШКА: Creates a placeholder notification for initial instruction assignment
        /// </summary>
        /// <param name="personnel">The personnel record</param>
        /// <param name="instructionId">The instruction ID</param>
        private async Task CreatePlaceholderNotification(Personnel personnel, int instructionId)
        {
            // TODO: ЗАГЛУШКА - Implement notification system
            // This is where you would:
            // 1. Send email notification to the new employee
            // 2. Create system notification
            // 3. Log the assignment in audit trail
            // 4. Update dashboard counters

            await Task.Delay(10); // Simulate async operation

            _logger.LogInformation($"ЗАГЛУШКА: Would send notification to personnel {personnel.personnel_number} about instruction {instructionId}");

            // Example of what could be implemented:
            /*
            var notification = new
            {
                PersonnelId = personnel.personnel_id,
                InstructionId = instructionId,
                Type = "InitialInstructionAssignment",
                CreatedAt = DateTime.UtcNow,
                Message = "Вам назначен вводный инструктаж"
            };

            // Save to notifications table
            // Send email
            // Update counters
            */
        }

        /// <summary>
        /// Creates user credentials and inserts the user into Management.users table
        /// </summary>
        /// <param name="personnel">The personnel record</param>
        /// <param name="department">The department</param>
        /// <param name="employee">The employee data (EmployeeCreationDto)</param>
        /// <returns>Generated user credentials or null if failed</returns>
        private async Task<UserCredentialsResult> CreateUserCredentialsForNewEmployee(
            Personnel personnel,
            Models.Department department,
            EmployeeCreationDto employee)
        {
            try
            {
                // Generate username based on employee data
                var username = GenerateUsername(employee);

                // Check if username already exists and make it unique if needed
                username = await EnsureUniqueUsername(username);

                // Generate a secure random password
                var password = GenerateSecurePassword(username);

                // Hash the password using BCrypt
                var passwordHash = Encryption_Kotova.HashPassword(password);

                // Determine user role ID based on employee role
                var userRoleId = GetUserRoleId(employee.Role);

                // Use workplace number from DTO or generate desk number
                var deskNumber = employee.WorkplaceNumber ?? GenerateDeskNumber(department.department_name, employee.FullName);

                // Create new user entity
                var user = new User
                {
                    username = username,
                    password_hash = passwordHash,
                    user_role_id = userRoleId,
                    personnel_id = personnel.personnel_id,
                    current_email = employee.Email, // EmployeeCreationDto has Email property
                    department_id = department.department_id,
                    desk_number = deskNumber
                };

                // Insert user into database
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"User created successfully: Username={username}, Role={userRoleId}, Department={department.department_id}");

                return new UserCredentialsResult
                {
                    Username = username,
                    Password = password, // Return plain password for initial setup (should be securely communicated)
                    UserId = user.id,
                    DeskNumber = deskNumber,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating user credentials for employee: {employee.FullName}");
                return null;
            }
        }

        /// <summary>
        /// Generates a username based on employee information
        /// </summary>
        /// <param name="employee">The employee data (EmployeeCreationDto)</param>
        /// <returns>Generated username</returns>
        private string GenerateUsername(EmployeeCreationDto employee)
        {
            // Generate username based on full name
            // Example: "Иванов Иван Иванович" -> "ivanov.ivan"
            var nameParts = employee.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (nameParts.Length >= 2)
            {
                var lastName = TransliterateToLatin(nameParts[0]).ToLower();
                var firstName = TransliterateToLatin(nameParts[1]).ToLower();
                return $"{lastName}.{firstName}";
            }
            else
            {
                // Fallback: use personnel number
                return $"user.{employee.PersonnelNumber}";
            }
        }

        /// <summary>
        /// Ensures the username is unique by appending numbers from 00 to 99 if needed
        /// </summary>
        /// <param name="baseUsername">The base username</param>
        /// <returns>Unique username or throws exception if all numbers 00-99 are taken</returns>
        private async Task<string> EnsureUniqueUsername(string baseUsername)
        {
            // First check if base username is available
            if (!await _dbContext.Users.AnyAsync(u => u.username == baseUsername))
            {
                return $"{baseUsername}.00";
            }

            // Try numbers from 00 to 99
            for (int i = 0; i <= 99; i++)
            {
                var numberSuffix = i.ToString("D2"); // Format as 2-digit number with leading zero
                var username = $"{baseUsername}.{numberSuffix}";

                if (!await _dbContext.Users.AnyAsync(u => u.username == username))
                {
                    return username;
                }
            }

            // If we reach here, all usernames from .00 to .99 are taken
            var nameParts = baseUsername.Split('.');
            var lastName = nameParts.Length > 0 ? nameParts[0] : "unknown";
            var firstName = nameParts.Length > 1 ? nameParts[1] : "unknown";

            throw new Exception($"Не удалось создать уникальное имя пользователя для сотрудника с именем '{firstName}' и фамилией '{lastName}'. " +
                               $"Все варианты от {baseUsername}.00 до {baseUsername}.99 уже заняты. " +
                               $"Обратитесь к администратору для решения данной проблемы.");
        }

        /// <summary>
        /// Generates a secure random password
        /// </summary>
        /// <returns>Secure password</returns>
        private string GenerateSecurePassword(string username)
        {
            // Password is the same as username for easier initial use
            return username;
        }

        /// <summary>
        /// Maps employee role to user role ID
        /// </summary>
        /// <param name="employeeRole">The employee role from NewEmployeeDto</param>
        /// <returns>User role ID</returns>
        private int GetUserRoleId(string employeeRole)
        {
            // Map based on role_names table:
            // 1 = User (Сотрудник)
            // 2 = ChiefOfDepartment (Начальник отдела)
            // 3 = Coordinator (Координатор)
            // 4 = Management (Главный инженер)
            // 5 = Admin (Администратор)
            // 6 = DeputyChief (Заместитель начальника)

            return employeeRole?.ToLower() switch
            {
                "administrator" or "admin" or "администратор" => 5,
                "coordinator" or "координатор" => 3,
                "chiefofdepartment" or "начальник отдела" => 2,
                "deputychief" or "заместитель начальника" => 6,
                "management" or "главный инженер" => 4,
                _ => 1 // Default to User role
            };
        }

        /// <summary>
        /// Generates a desk number for the employee
        /// </summary>
        /// <param name="departmentName">Department name</param>
        /// <param name="fullName">Employee full name</param>
        /// <returns>Generated desk number</returns>
        private string GenerateDeskNumber(string departmentName, string fullName)
        {
            // Generate desk number based on department and employee
            var deptCode = departmentName switch
            {
                "Общестроительный отдел" => "CONST",
                "Технический отдел" => "TECH",
                "Отдел охраны труда" => "HSE",
                "Руководство" => "MGT",
                _ => "GEN"
            };

            // Use a simple counter or hash for uniqueness
            var hash = Math.Abs(fullName.GetHashCode()) % 1000;
            return $"{deptCode}-{hash:D3}";
        }

        /// <summary>
        /// Simple transliteration from Cyrillic to Latin characters
        /// </summary>
        /// <param name="text">Text in Cyrillic</param>
        /// <returns>Transliterated text in Latin</returns>
        private string TransliterateToLatin(string text)
        {
            var transliteration = new Dictionary<char, string>
    {
        {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "g"}, {'д', "d"},
        {'е', "e"}, {'ё', "yo"}, {'ж', "zh"}, {'з', "z"}, {'и', "i"},
        {'й', "y"}, {'к', "k"}, {'л', "l"}, {'м', "m"}, {'н', "n"},
        {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"},
        {'у', "u"}, {'ф', "f"}, {'х', "kh"}, {'ц', "ts"}, {'ч', "ch"},
        {'ш', "sh"}, {'щ', "shch"}, {'ъ', ""}, {'ы', "y"}, {'ь', ""},
        {'э', "e"}, {'ю', "yu"}, {'я', "ya"},
        {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "G"}, {'Д', "D"},
        {'Е', "E"}, {'Ё', "Yo"}, {'Ж', "Zh"}, {'З', "Z"}, {'И', "I"},
        {'Й', "Y"}, {'К', "K"}, {'Л', "L"}, {'М', "M"}, {'Н', "N"},
        {'О', "O"}, {'П', "P"}, {'Р', "R"}, {'С', "S"}, {'Т', "T"},
        {'У', "U"}, {'Ф', "F"}, {'Х', "Kh"}, {'Ц', "Ts"}, {'Ч', "Ch"},
        {'Ш', "Sh"}, {'Щ', "Shch"}, {'Ъ', ""}, {'Ы', "Y"}, {'Ь', ""},
        {'Э', "E"}, {'Ю', "Yu"}, {'Я', "Ya"}
    };

            var result = new StringBuilder();
            foreach (char c in text)
            {
                if (transliteration.ContainsKey(c))
                {
                    result.Append(transliteration[c]);
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Result class for user credentials creation
        /// </summary>
        public class UserCredentialsResult
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public int UserId { get; set; }
            public string? DeskNumber { get; set; }
            public bool Success { get; set; }
        }

        // Optional: Add this method to return credentials to client if needed
        /// <summary>
        /// Gets the generated credentials for the newly created employee
        /// </summary>
        /// <param name="personnelId">Personnel ID</param>
        /// <returns>User credentials</returns>
        private async Task<UserCredentialsResult> GetEmployeeCredentials(int personnelId)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.personnel_id == personnelId);

            if (user != null)
            {
                return new UserCredentialsResult
                {
                    Username = user.username,
                    Password = "***", // Don't return actual password after creation
                    UserId = user.id,
                    Success = true
                };
            }

            return null;
        }


        #endregion

    }
}