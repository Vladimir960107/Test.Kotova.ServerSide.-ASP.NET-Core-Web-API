using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Kotova.CommonClasses;
using Microsoft.EntityFrameworkCore.Internal;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TelpEmployeesController : ControllerBase
    {
        private readonly TelpDbContext _context;
        private readonly ApplicationDbContextUsers _userContext;

        public TelpEmployeesController(TelpDbContext context, ApplicationDbContextUsers userContext)
        {
            _context = context;
            _userContext = userContext;
        }

        /// <summary>
        /// Retrieves all employees with their department and position information.
        /// </summary>
        [HttpGet("get-all-employees")]
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<ActionResult<IEnumerable<TelpEmployeeDto>>> GetAllEmployees()
        {
            var employees = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Where(e => !string.IsNullOrEmpty(e.PersonnelNumber))
                .Select(e => new TelpEmployeeDto
                {
                    FullName = e.FullName,
                    DepartmentName = e.Department.Name,
                    PositionName = e.Position.Name,
                    Email = e.Email,
                    PersonnelNumber = e.PersonnelNumber
                })
                .ToListAsync();

            return Ok(employees);
        }

        [HttpGet("get-filtered-employees")] 
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<ActionResult<IEnumerable<TelpEmployeeDto>>> GetFilteredEmployees()
        {
            if (_userContext.Users == null)
            {
                // Return a 404 Not Found response if Users is null.
                return NotFound("The Users data is not available.");
            }

            List<byte> codeNumbers = await _userContext.Departments
                .Select(u => u.code_number_TELP_DB)
                .ToListAsync();
            codeNumbers.Remove(15); //ВАЖНО! УБИРАЕМ НАЧАЛЬСТВО. TODO: Вернуть начальство когда нужно будет ;)

            HashSet<byte> codeNumbersSet = new HashSet<byte>(codeNumbers);
            //TODO: CONTINUE!!!!!!!!!!!!!!!!!!!!!!!!! 03.01.25-04.01.25 Создай фильтр только тех personnel number которые в списке codeNumbers ;0

            List<string> personnelNumbersAlreadyInUse = await _userContext.Users
                .Select(u => u.current_personnel_number)
                .Where(pn => !string.IsNullOrEmpty(pn))
                .ToListAsync();
            // Convert the list to a HashSet for fast lookup.
            var personnelNumbersSet = new HashSet<string>(personnelNumbersAlreadyInUse);


            var filteredEmployees = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Where(e =>
                       !string.IsNullOrEmpty(e.PersonnelNumber) &&
                       // Exclude employees whose personnel number is in the "already in use" set.
                       !personnelNumbersSet.Contains(e.PersonnelNumber) &&
                       codeNumbersSet.Contains(checked((byte)(e.DepartmentId ?? 0))))
                .Select(e => new TelpEmployeeDto
                {
                    FullName = e.FullName,
                    DepartmentName = e.Department.Name,
                    PositionName = e.Position.Name,
                    Email = e.Email,
                    PersonnelNumber = e.PersonnelNumber
                })
                .ToListAsync();

            if (filteredEmployees == null || !filteredEmployees.Any())
            {
                return NotFound("No filtered employees found.");
            }
            return Ok(filteredEmployees);
        }

        /// <summary>
        /// Retrieves an employee by their personnel number.
        /// </summary>
        [HttpGet("get-by-personnel-number/{personnelNumber}")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<TelpEmployeeDto>> GetEmployeeByPersonnelNumber(string personnelNumber)
        {
            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Position)
                .Where(e => e.PersonnelNumber == personnelNumber)
                .Select(e => new TelpEmployeeDto
                {
                    FullName = e.FullName,
                    DepartmentName = e.Department.Name,
                    PositionName = e.Position.Name,
                    Email = e.Email,
                    PersonnelNumber = e.PersonnelNumber
                })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return NotFound($"Employee with personnel number {personnelNumber} not found.");
            }

            return Ok(employee);
        }
    }
}