using DocumentFormat.OpenXml.Bibliography;
using Kotova.CommonClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using System.Security.Claims;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly MyDataService _dataService;
        private readonly ApplicationDBContextGeneralConstr _contextGeneralConstr;
        private readonly ApplicationDbContextUsers _userContext;
        private readonly ApplicationDBContextTechnicalDepartment _contextTechnicalDepartment;
        private readonly ApplicationDBContextManagement _contextManagement;

        public TasksController(MyDataService dataService, ApplicationDBContextGeneralConstr contextGeneralConstr, ApplicationDbContextUsers userContext, ApplicationDBContextTechnicalDepartment contextTechnicalDepartment, ApplicationDBContextManagement contextManagement)
        {
            _dataService = dataService;
            _contextGeneralConstr = contextGeneralConstr;
            _userContext = userContext;
            _contextTechnicalDepartment = contextTechnicalDepartment;
            _contextManagement = contextManagement;
        }

        /*[Authorize]
        [HttpGet("create-random-task")]
        public async Task<IActionResult> CreateTask()
        {
            // Generate some random data for the task
            var random = new Random();
            string[] descriptions = new[]
            {
            "Complete the project proposal",
            "Organize a department meeting",
            "Submit the quarterly report",
            "Update the department policy",
            "Review performance evaluations"
            };

            string description = descriptions[random.Next(descriptions.Length)];

            // Get department IDs from the Departments table
            int[] departmentIds = await _userContext.Departments.Select(d => d.department_id).ToArrayAsync();
            int departmentId = departmentIds[random.Next(departmentIds.Length)];

            // Get user roles (this should match the `UserRole` enum or IDs from your system)
            int[] userRoles = await _userContext.Roles.Select(r => r.roleid).ToArrayAsync(); 
            int? userRole = userRoles[random.Next(userRoles.Length)];

            // Assign random due date (within the next 30 days)
            DateTime? dueDate = DateTime.Now.AddDays(random.Next(1, 31));

            // Create a new task for the user
            var newTask = new TaskForUser
            {
                Description = description,
                DepartmentId = departmentId,
                UserRole = userRole,  // Assign the user role
                AssignedTo = null,    // No specific user assigned
                CreatedAt = DateTime.Now,
                DueDate = dueDate,
                Status = "Не назначено"
            };

            // Add the task to the database
            _userContext.Tasks.Add(newTask);
            await _userContext.SaveChangesAsync();

            return Ok(newTask);
        }*/



        /// <summary>
        /// Creates a custom task (Test Function).
        /// </summary>
        /// <remarks>
        /// This endpoint allows an Administrator to create a custom task and save it to the database. 
        /// **Note:** This is a test function and is not currently in use in the production environment.
        /// </remarks>
        /// <param name="someInfoAboutNewUser">
        /// An object containing information about the custom task, including the description, 
        /// department ID, assigned user, due date, and status.
        /// </param>
        /// <returns>
        /// Returns an OK response with the created task object if the operation is successful. 
        /// Returns a BadRequest response if there is an error during processing.
        /// </returns>
        /// <response code="200">
        /// The custom task was successfully created.
        /// </response>
        /// <response code="400">
        /// A bad request occurred due to an error during task creation.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        [Authorize(Roles = "Administrator")]
        [HttpPost("create-custom-task")]
        public async Task<IActionResult> CreateCustomTask([FromBody] CustomTask someInfoAboutNewUser)
        {
            try
            {
                var newTask = new TaskForUser
                {
                    Description = someInfoAboutNewUser.Description,
                    DepartmentId = someInfoAboutNewUser.DepartmentId,
                    UserRole = someInfoAboutNewUser.UserRole,  // Assign the user role
                    AssignedTo = someInfoAboutNewUser.AssignedTo,    // No specific user assigned
                    CreatedAt = DateTime.Now,
                    DueDate = someInfoAboutNewUser.DueDate,
                    Status = someInfoAboutNewUser.Status,
                };
                _userContext.Tasks.Add(newTask);
                await _userContext.SaveChangesAsync();

                return Ok(newTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest(ex.Message);
                
            }
            
        }



        /// <summary>
        /// Retrieves all current tasks for a Chief of Department (Test Function).
        /// </summary>
        /// <remarks>
        /// This endpoint allows a Chief of Department to retrieve all tasks assigned to their department. 
        /// Tasks are filtered by department ID and user role (Chief of Department). 
        /// **Note:** This is a test function and is not currently in use in the production environment.
        /// </remarks>
        /// <returns>
        /// Returns an OK response with a list of tasks if the operation is successful. 
        /// Returns a BadRequest response if there is an error during processing.
        /// </returns>
        /// <response code="200">
        /// The tasks were successfully retrieved.
        /// </response>
        /// <response code="400">
        /// A bad request occurred due to an error during task retrieval.
        /// </response>
        /// <response code="401">
        /// Unauthorized - The user is not authenticated.
        /// </response>
        /// <response code="403">
        /// Forbidden - The user does not have the required role.
        /// </response>
        [Authorize(Roles = "ChiefOfDepartment")]
        [HttpGet("get-all-current-tasks-for-chief")]
        public async Task<IActionResult> GetAllCurrentTasksForChief()
        {
            try
            {
                int chiefDepartmentId = int.Parse(User.FindFirst("department_id")?.Value);

                var result = await _userContext.Tasks
                    .Where(t => t.DepartmentId == chiefDepartmentId && t.UserRole == 2) // 2 indicates ChiefOfDepartment
                    .Select(t => new TaskDto
                    {
                        TaskId = t.TaskId,
                        Description = t.Description
                    })
                    .ToListAsync();
                return Ok(result);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest(ex.Message);

            }
        }

    }
}
