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
using Task = System.Threading.Tasks.Task;
using Kotova.CommonClasses;
using System.Globalization;

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

                // Get the department ID for the current user
                int departmentId = user.department_id;

                // Process the instruction assignment
                return await ProcessInstructionAssignment(package, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending instruction to names");
                return BadRequest($"An error occurred while processing the instruction: {ex.Message}");
            }
        }

        private async Task<IActionResult> ProcessInstructionAssignment(InstructionPackage package, User currentUser)
        {
            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    // Find the instruction by its cause
                    var instruction = await _dbContext.Instructions
                        .FirstOrDefaultAsync(i => i.cause_of_instruction == package.InstructionCause);

                    if (instruction == null)
                    {
                        return NotFound($"Instruction with cause '{package.InstructionCause}' not found.");
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

                    // Find matching personnel records
                    var personnel = await _dbContext.Personnel
                        .Join(_dbContext.EmployeesByDepartment,
                            p => p.personnel_id,
                            e => e.personnel_id,
                            (p, e) => new { Personnel = p, Employee = e })
                        .Where(x => namesList.Contains(x.Employee.full_name) &&
                                   birthdatesList.Contains(x.Employee.birth_date) &&
                                   x.Employee.department_id == currentUser.department_id)
                        .Select(x => x.Personnel)
                        .ToListAsync();

                    if (!personnel.Any())
                    {
                        return BadRequest("No matching personnel found for the provided names and birthdates.");
                    }

                    // Create instruction status records for each personnel
                    foreach (var person in personnel)
                    {
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
                        }
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Send notifications to the assigned personnel
                    foreach (var person in personnel)
                    {
                        var userWithPersonnel = await _dbContext.Users
                            .FirstOrDefaultAsync(u => u.personnel_id == person.personnel_id);

                        if (userWithPersonnel != null)
                        {
                            await _notificationsService.SendUserNotificationAsync(
                                userWithPersonnel.id,
                                $"You have been assigned a new instruction: {instruction.cause_of_instruction}",
                                currentUser.id);
                        }
                    }

                    return Ok($"Instruction '{package.InstructionCause}' has been assigned to {personnel.Count} people.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing instruction assignment");
                    throw;
                }
            }
        }
    }

    #endregion
}