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
        private InstructionResultDto MapToDto(Models.Instruction instruction, List<string> filePaths)
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
                TypeName = GetInstructionTypeName(instruction.type_of_instruction)
            };
        }

        // Helper method to get the instruction type name
        private string GetInstructionTypeName(byte typeCode)
        {
            return typeCode switch
            {
                0 => "Вводный",
                1 => "Внеплановый",
                2 => "Первичный",
                3 => "Повторный",
                4 => "Повторный (для водителей)",
                5 => "Целевой",
                _ => "Неизвестный тип"
            };
        }
        #endregion

        #region UserRegion

        /// <summary>
        /// Retrieves not passed instructions for the authenticated user.
        /// </summary>
        /// <remarks>
        /// This endpoint fetches the user's associated not passed instructions by determining their personnel number 
        /// and department ID from the database.
        /// Requires the user to be authenticated.
        /// </remarks>
        /// <returns>
        /// Returns a list of instruction data for the user.
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

                // Get instruction statuses for the user that are not passed
                var instructionStatuses = await _dbContext.InstructionStatuses
                    .Where(s => s.personnel_id == user.personnel_id && !s.is_instruction_passed)
                    .Include(s => s.Instruction)
                    .ThenInclude(i => i.FilePaths)
                    .Include(s => s.Instruction)
                    .ThenInclude(i => i.InstructionType)
                    .ToListAsync();

                var result = instructionStatuses.Select(s => new
                {
                    InstructionId = s.instruction_id,
                    Cause = s.Instruction.cause_of_instruction,
                    BeginDate = s.Instruction.begin_date,
                    EndDate = s.Instruction.end_date,
                    Type = s.Instruction.InstructionType?.name_of_type_instruction,
                    WhenAssigned = s.when_was_sent_to_user,
                    FilePaths = s.Instruction.FilePaths.Select(fp => fp.file_path).ToList()
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
        /// Retrieves passed instructions for the authenticated user.
        /// </summary>
        /// <remarks>
        /// This endpoint fetches the user's associated passed instructions by determining their personnel number 
        /// and department ID from the database.
        /// Requires the user to be authenticated.
        /// </remarks>
        /// <returns>
        /// Returns a list of instruction data for the user.
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

                // Get instruction statuses for the user that are passed
                var instructionStatuses = await _dbContext.InstructionStatuses
                    .Where(s => s.personnel_id == user.personnel_id && s.is_instruction_passed)
                    .Include(s => s.Instruction)
                    .ThenInclude(i => i.FilePaths)
                    .Include(s => s.Instruction)
                    .ThenInclude(i => i.InstructionType)
                    .ToListAsync();

                var result = instructionStatuses.Select(s => new
                {
                    InstructionId = s.instruction_id,
                    Cause = s.Instruction.cause_of_instruction,
                    BeginDate = s.Instruction.begin_date,
                    EndDate = s.Instruction.end_date,
                    Type = s.Instruction.InstructionType?.name_of_type_instruction,
                    WhenPassed = s.date_when_passed,
                    FilePaths = s.Instruction.FilePaths.Select(fp => fp.file_path).ToList()
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
            return Ok("Привет, мир!");
        }

        [HttpGet("sync-instructions-with-db")]
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
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
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
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
                        e.job_position != "Начальник отдела" &&
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
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
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
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim not found.");
                }

                var user = await _dbContext.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.username == username);

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

                        // Mark the instruction as assigned to people
                        instruction.is_assigned_to_people = true;
                        _dbContext.Instructions.Update(instruction);
                        await _dbContext.SaveChangesAsync();

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
                                (p, e) => new {
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
        /// <summary>
        /// Adds a new instruction into the database.
        /// </summary>
        /// <remarks>
        /// This endpoint allows authorized users (Chiefs of Departments or Administrators) to add a new instruction 
        /// to the database. The instruction details include cause, date range, type, and file paths.
        /// </remarks>
        /// <param name="request">The instruction details to be added</param>
        /// <returns>
        /// Returns the created instruction if successful, or appropriate error responses.
        /// </returns>
        /// <response code="200">The instruction was successfully added to the database</response>
        /// <response code="400">The instruction data is invalid or there was an error processing the request</response>
        /// <response code="401">The user is not authenticated</response>
        /// <response code="403">The user doesn't have permission to add instructions</response>
        [HttpPost("add-new-instruction-into-db")]
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> AddNewInstructionIntoDB([FromBody] AddInstructionRequest request)
        {
            try
            {
                if (request == null || request.Instruction == null)
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
                    cause_of_instruction = request.Instruction.CauseOfInstruction,
                    begin_date = DateTime.UtcNow, // Always use current time for begin_date
                    end_date = request.Instruction.EndDate,
                    type_of_instruction = request.Instruction.TypeOfInstruction,
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

                                // Track added file paths
                                List<string> addedFilePaths = new List<string>();

                                // Add file paths if they exist
                                if (request.Paths != null && request.Paths.Any())
                                {
                                    foreach (var path in request.Paths)
                                    {
                                        var filePathEntity = new FilePathForInstruction
                                        {
                                            instruction_id = instruction.instruction_id,
                                            file_path = path,
                                            instruction_name = instruction.cause_of_instruction
                                        };

                                        _dbContext.FilePathsForInstructions.Add(filePathEntity);
                                        addedFilePaths.Add(path);
                                    }

                                    await _dbContext.SaveChangesAsync();
                                }
                                // If there's a path_to_instruction but no paths, add it as a file path
                                else if (!string.IsNullOrEmpty(request.Instruction.PathToInstruction))
                                {
                                    var filePathEntity = new FilePathForInstruction
                                    {
                                        instruction_id = instruction.instruction_id,
                                        file_path = request.Instruction.PathToInstruction,
                                        instruction_name = instruction.cause_of_instruction
                                    };

                                    _dbContext.FilePathsForInstructions.Add(filePathEntity);
                                    addedFilePaths.Add(request.Instruction.PathToInstruction);
                                    await _dbContext.SaveChangesAsync();
                                }

                                await transaction.CommitAsync();

                                // Map entity to DTO for response
                                return MapToDto(instruction, addedFilePaths);
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
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
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
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
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
                    .Where(i => i.department_id == user.department_id)
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
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
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
        /// <remarks>
        /// This endpoint allows authorized users to update an existing instruction's details.
        /// </remarks>
        /// <param name="id">The ID of the instruction to update</param>
        /// <param name="instructionDto">The updated instruction data</param>
        /// <returns>
        /// The updated instruction if successful.
        /// </returns>
        /// <response code="200">The instruction was successfully updated.</response>
        /// <response code="400">Bad request - The provided data was invalid.</response>
        /// <response code="401">Unauthorized - The user is not authenticated.</response>
        /// <response code="403">Forbidden - The user does not have the required role.</response>
        /// <response code="404">Not Found - The specified instruction was not found.</response>
        /// <response code="500">Internal server error occurred during update.</response>
        [HttpPut("update-instruction/{id}")]
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> UpdateInstruction(int id, [FromBody] UpdateInstructionDto instructionDto)
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

                // Handle file paths update if needed
                if (instructionDto.FilePaths != null)
                {
                    // Remove existing file paths
                    var existingPaths = await _dbContext.FilePathsForInstructions
                        .Where(fp => fp.instruction_id == id)
                        .ToListAsync();

                    _dbContext.FilePathsForInstructions.RemoveRange(existingPaths);
                    await _dbContext.SaveChangesAsync();

                    // Add new file paths
                    foreach (var path in instructionDto.FilePaths)
                    {
                        var filePathEntity = new FilePathForInstruction
                        {
                            instruction_id = id,
                            file_path = path,
                            instruction_name = instruction.cause_of_instruction
                        };

                        _dbContext.FilePathsForInstructions.Add(filePathEntity);
                    }

                    await _dbContext.SaveChangesAsync();
                }

                // Return the updated instruction
                var updatedInstruction = await _dbContext.Instructions
                    .Include(i => i.InstructionType)
                    .Include(i => i.FilePaths)
                    .FirstOrDefaultAsync(i => i.instruction_id == id);

                var result = new
                {
                    updatedInstruction.instruction_id,
                    updatedInstruction.cause_of_instruction,
                    updatedInstruction.begin_date,
                    updatedInstruction.end_date,
                    updatedInstruction.type_of_instruction,
                    updatedInstruction.is_assigned_to_people,
                    updatedInstruction.is_passed_by_everyone,
                    TypeName = updatedInstruction.InstructionType?.name_of_type_instruction,
                    FilePaths = updatedInstruction.FilePaths.Select(fp => fp.file_path).ToList()
                };

                return Ok(result);
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
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
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
    }
}