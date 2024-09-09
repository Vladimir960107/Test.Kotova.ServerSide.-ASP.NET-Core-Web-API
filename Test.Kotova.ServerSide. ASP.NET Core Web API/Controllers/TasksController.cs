using Kotova.CommonClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        [Authorize]
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
        }
    }
}
