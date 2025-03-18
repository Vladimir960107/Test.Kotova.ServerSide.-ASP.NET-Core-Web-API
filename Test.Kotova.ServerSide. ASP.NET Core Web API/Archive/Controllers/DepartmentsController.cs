using Microsoft.AspNetCore.Mvc;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DepartmentsController : ControllerBase
    {
        private readonly ILynksDbService _dbService;
        private readonly ILogger<DepartmentsController> _logger;

        public DepartmentsController(
            ILynksDbService dbService,
            ILogger<DepartmentsController> logger)
        {
            _dbService = dbService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Department>>> GetAllDepartments()
        {
            try
            {
                var departments = await _dbService.GetAllDepartmentsAsync();
                return Ok(departments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Department>> GetDepartment(int id)
        {
            try
            {
                var department = await _dbService.GetDepartmentByIdAsync(id);

                if (department == null)
                    return NotFound($"Department with ID {id} not found");

                return Ok(department);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving department with ID {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{departmentId}/employees")]
        public async Task<ActionResult<IEnumerable<EmployeeByDepartment>>> GetEmployeesByDepartment(int departmentId)
        {
            try
            {
                var employees = await _dbService.GetEmployeesByDepartmentAsync(departmentId);
                return Ok(employees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving employees for department ID {departmentId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{departmentId}/instructions")]
        public async Task<ActionResult<IEnumerable<Instruction>>> GetInstructionsByDepartment(int departmentId)
        {
            try
            {
                var instructions = await _dbService.GetInstructionsByDepartmentAsync(departmentId);
                return Ok(instructions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving instructions for department ID {departmentId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Department>> CreateDepartment([FromBody] Department department)
        {
            try
            {
                if (department == null)
                    return BadRequest("Department cannot be null");

                var createdDepartment = await _dbService.CreateDepartmentAsync(department);
                return CreatedAtAction(nameof(GetDepartment), new { id = createdDepartment.department_id }, createdDepartment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating department");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Department>> UpdateDepartment(int id, [FromBody] Department department)
        {
            try
            {
                if (department == null)
                    return BadRequest("Department cannot be null");

                if (id != department.department_id)
                    return BadRequest("Department ID mismatch");

                var existingDepartment = await _dbService.GetDepartmentByIdAsync(id);
                if (existingDepartment == null)
                    return NotFound($"Department with ID {id} not found");

                var updatedDepartment = await _dbService.UpdateDepartmentAsync(department);
                return Ok(updatedDepartment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating department with ID {id}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteDepartment(int id)
        {
            try
            {
                var result = await _dbService.DeleteDepartmentAsync(id);
                if (!result)
                    return NotFound($"Department with ID {id} not found");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting department with ID {id}");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
