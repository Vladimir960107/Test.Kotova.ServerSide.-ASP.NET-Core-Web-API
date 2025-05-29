using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using Kotova.CommonClasses;
using Task = System.Threading.Tasks.Task;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage;
using System.ComponentModel.DataAnnotations;

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
        /// Marks an instruction as passed by the authenticated user.
        /// </summary>
        /// <remarks>
        /// This endpoint marks a specific instruction as passed by the authenticated user.
        /// Requires the user to be authenticated.
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

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Personnel)
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
                    .FirstOrDefaultAsync(s => s.instruction_id == instructionId && s.personnel_id == user.personnel_id);

                if (instructionStatus == null)
                {
                    return NotFound($"Instruction with ID {instructionId} not found for this user");
                }

                // Mark as passed
                instructionStatus.is_instruction_passed = true;
                instructionStatus.date_when_passed = DateTime.Now;
                instructionStatus.date_when_passed_UTC = DateTime.UtcNow;

                // Save changes
                await _dbContext.SaveChangesAsync();

                // Check if all personnel have passed this instruction
                await CheckAndUpdateInstructionCompletionStatus(instructionId);

                return Ok("Instruction successfully marked as passed");
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
        /// </remarks>
        /// <param name="instructionId">The ID of the instruction to report on</param>
        /// <returns>A report with sorted employee compliance data</returns>
        [HttpGet("get-instruction-compliance-report/{instructionId}")]
        [Authorize(Roles = "ChiefOfDepartment, DeputyChief, Administrator")]
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
                    .FirstOrDefaultAsync(u => u.id == userIdInt);

                if (user == null)
                {
                    return BadRequest("User not found");
                }

                int departmentId = user.department_id;

                // Get the specific instruction
                var instruction = await _dbContext.Instructions
                    .Where(i => i.instruction_id == instructionId && i.department_id == departmentId)
                    .Include(i => i.InstructionType)
                    .FirstOrDefaultAsync();

                if (instruction == null)
                {
                    return NotFound($"Instruction with ID {instructionId} not found");
                }

                // Get all instruction statuses for this instruction
                var statuses = await _dbContext.InstructionStatuses
                    .Where(s => s.instruction_id == instructionId && s.department_id == departmentId)
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

                        // Get the assigner's name and job position
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
        public async Task<IActionResult> GetNormativeInstructions()
        {
            try
            {
                var normativeInstructions = await _dbContext.NormativeInstructionNames.ToListAsync();

                // Map to DTOs explicitly to ensure proper property mapping
                var result = normativeInstructions.Select(ni => new
                {
                    Id = ni.id,
                    Name = ni.normative_instruction_name,
                    Url = ni.url,
                    CreatedAt = ni.created_at
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
        public async Task<IActionResult> CreateNormativeInstruction([FromBody] NormativeInstructionCreateModel model)
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
                    created_at = DateTime.UtcNow
                };

                _dbContext.NormativeInstructionNames.Add(normativeInstruction);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetNormativeInstructions), null, normativeInstruction);
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

                // Get all instructions for the department
                var instructions = await _dbContext.Instructions
                    .Where(i => i.department_id == user.department_id && !i.is_assigned_to_people)
                    .Include(i => i.InstructionType)
                    .Include(i => i.FilePaths)
                    .ToListAsync();

                // Map to DTOs
                var result = instructions.Select(i => new
                {
                    i.instruction_id,
                    i.cause_of_instruction,
                    i.begin_date,
                    i.end_date,
                    i.type_of_instruction,
                    i.is_assigned_to_people,
                    i.is_passed_by_everyone,
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

        // Add this new method to your InstructionsController.cs
        // Replace the existing assign-unplanned-instruction-to-chiefs method

        /// <summary>
        /// Assigns an unplanned instruction to all chiefs in selected departments
        /// </summary>
        /// <remarks>
        /// This endpoint creates an unplanned instruction and assigns it to all chiefs and deputy chiefs
        /// in the selected departments. Only accessible by Management role.
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
                                        is_assigned_to_people = true,
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

                                        // Add normative instructions if provided
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
                    .Where(i => i.type_of_instruction == 1 && i.is_assigned_to_people)
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

        #endregion

        #endregion
    }
}