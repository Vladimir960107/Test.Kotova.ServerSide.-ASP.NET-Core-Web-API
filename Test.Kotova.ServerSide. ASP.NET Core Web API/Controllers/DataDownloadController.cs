using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using System.ComponentModel.DataAnnotations;
using Kotova.CommonClasses;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataDownloadController : ControllerBase
    {
        private readonly LynksDbContext _dbContext;
        private readonly ILogger<DataDownloadController> _logger;

        public DataDownloadController(LynksDbContext dbContext, ILogger<DataDownloadController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Downloads all departments from the database
        /// </summary>
        /// <remarks>
        /// This endpoint retrieves all departments from Management.departments_names table.
        /// Returns department ID, name, and other relevant information.
        /// Accessible by Management and Administrator roles.
        /// </remarks>
        /// <returns>List of all departments</returns>
        /// <response code="200">Successfully retrieved all departments</response>
        /// <response code="401">Unauthorized - User is not authenticated</response>
        /// <response code="403">Forbidden - User does not have required permissions</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("departments")]
        [Authorize(Roles = "Management, Coordinator, Administrator")]
        public async Task<IActionResult> GetAllDepartments()
        {
            try
            {
                _logger.LogInformation("Retrieving all departments for data download");

                var departments = await _dbContext.Departments
                    .Select(d => new DepartmentDownloadDto
                    {
                        DepartmentId = d.department_id,
                        DepartmentName = d.department_name,
                        IsChiefOnline = d.is_chief_online,
                        LastOnlineSetUTC = d.last_online_set_UTC,
                        CodeNumberTelpDb = d.code_number_TELP_DB
                    })
                    .OrderBy(d => d.DepartmentId)
                    .ToListAsync();

                _logger.LogInformation($"Successfully retrieved {departments.Count} departments");

                return Ok(new
                {
                    Success = true,
                    Count = departments.Count,
                    Data = departments,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments for download");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while retrieving departments"
                });
            }
        }

        /// <summary>
        /// Downloads all roles from the database
        /// </summary>
        /// <remarks>
        /// This endpoint retrieves all roles from Management.role_names table.
        /// Returns role ID, type, and Russian name.
        /// Accessible by Management and Administrator roles.
        /// </remarks>
        /// <returns>List of all roles</returns>
        /// <response code="200">Successfully retrieved all roles</response>
        /// <response code="401">Unauthorized - User is not authenticated</response>
        /// <response code="403">Forbidden - User does not have required permissions</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("roles")]
        [Authorize(Roles = "Management, Coordinator, Administrator")]
        public async Task<IActionResult> GetAllRoles()
        {
            try
            {
                _logger.LogInformation("Retrieving all roles for data download");

                var roles = await _dbContext.Roles
                    .Select(r => new RoleDownloadDto
                    {
                        RoleId = r.role_id,
                        RoleType = r.role_type,
                        RoleNameRussian = r.role_name_russian
                    })
                    .OrderBy(r => r.RoleId)
                    .ToListAsync();

                _logger.LogInformation($"Successfully retrieved {roles.Count} roles");

                return Ok(new
                {
                    Success = true,
                    Count = roles.Count,
                    Data = roles,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles for download");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while retrieving roles"
                });
            }
        }

        /// <summary>
        /// Downloads both departments and roles in a single request
        /// </summary>
        /// <remarks>
        /// This endpoint retrieves both departments and roles data in one call for efficiency.
        /// Useful when you need both datasets together.
        /// Accessible by Management and Administrator roles.
        /// </remarks>
        /// <returns>Combined departments and roles data</returns>
        /// <response code="200">Successfully retrieved departments and roles</response>
        /// <response code="401">Unauthorized - User is not authenticated</response>
        /// <response code="403">Forbidden - User does not have required permissions</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("departments-and-roles")]
        [Authorize(Roles = "Management, Coordinator, Administrator")]
        public async Task<IActionResult> GetDepartmentsAndRoles()
        {
            try
            {
                _logger.LogInformation("Retrieving departments and roles for combined data download");

                var departmentsTask = _dbContext.Departments
                    .Select(d => new DepartmentDownloadDto
                    {
                        DepartmentId = d.department_id,
                        DepartmentName = d.department_name,
                        IsChiefOnline = d.is_chief_online,
                        LastOnlineSetUTC = d.last_online_set_UTC,
                        CodeNumberTelpDb = d.code_number_TELP_DB
                    })
                    .OrderBy(d => d.DepartmentId)
                    .ToListAsync();

                var rolesTask = _dbContext.Roles
                    .Select(r => new RoleDownloadDto
                    {
                        RoleId = r.role_id,
                        RoleType = r.role_type,
                        RoleNameRussian = r.role_name_russian
                    })
                    .OrderBy(r => r.RoleId)
                    .ToListAsync();

                await Task.WhenAll(departmentsTask, rolesTask);

                var departments = await departmentsTask;
                var roles = await rolesTask;

                _logger.LogInformation($"Successfully retrieved {departments.Count} departments and {roles.Count} roles");

                return Ok(new
                {
                    Success = true,
                    Departments = new
                    {
                        Count = departments.Count,
                        Data = departments
                    },
                    Roles = new
                    {
                        Count = roles.Count,
                        Data = roles
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments and roles for combined download");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while retrieving departments and roles"
                });
            }
        }
    }

    
}