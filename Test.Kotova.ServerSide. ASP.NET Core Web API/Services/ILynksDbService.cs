using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    public interface ILynksDbService
    {
        // Department methods
        Task<User> UpdateUserAsync(User user);
        Task<IEnumerable<Department>> GetAllDepartmentsAsync();
        Task<Department> GetDepartmentByIdAsync(int departmentId);
        Task<Department> CreateDepartmentAsync(Department department);
        Task<Department> UpdateDepartmentAsync(Department department);
        Task<bool> DeleteDepartmentAsync(int departmentId);

        // Personnel methods
        Task<IEnumerable<Personnel>> GetAllPersonnelAsync();
        Task<Personnel> GetPersonnelByIdAsync(int personnelId);
        Task<Personnel> GetPersonnelByNumberAsync(string personnelNumber);
        Task<Personnel> CreatePersonnelAsync(Personnel personnel);

        // Employee methods
        Task<string> GetEmployeeFullNameAsync(int personnelId, int departmentId);
        Task<IEnumerable<EmployeeByDepartment>> GetEmployeesByDepartmentAsync(int departmentId);
        Task<EmployeeByDepartment> CreateEmployeeAsync(EmployeeByDepartment employee);

        // Instruction methods
        Task<IEnumerable<Instruction>> GetInstructionsByDepartmentAsync(int departmentId);
        Task<Instruction> GetInstructionByIdAsync(int instructionId);
        Task<Instruction> CreateInstructionAsync(Instruction instruction);
        Task<bool> MarkInstructionAsPassedAsync(int instructionId, int personnelId);

        // User methods
        Task<User> GetUserByUsernameAsync(string username);
        Task<User> CreateUserAsync(User user);
    }

    public class LynksDbService : ILynksDbService
    {
        private readonly LynksDbContext _context;

        public LynksDbService(LynksDbContext context)
        {
            _context = context;
        }

        // Department methods
        public async Task<IEnumerable<Department>> GetAllDepartmentsAsync()
        {
            return await _context.Departments.ToListAsync();
        }

        public async Task<Department> GetDepartmentByIdAsync(int departmentId)
        {
            return await _context.Departments.FindAsync(departmentId);
        }

        public async Task<Department> CreateDepartmentAsync(Department department)
        {
            _context.Departments.Add(department);
            await _context.SaveChangesAsync();
            return department;
        }

        public async Task<Department> UpdateDepartmentAsync(Department department)
        {
            _context.Entry(department).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return department;
        }

        public async Task<bool> DeleteDepartmentAsync(int departmentId)
        {
            var department = await _context.Departments.FindAsync(departmentId);
            if (department == null)
                return false;

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            return true;
        }

        // Personnel methods
        public async Task<IEnumerable<Personnel>> GetAllPersonnelAsync()
        {
            return await _context.Personnel.ToListAsync();
        }

        public async Task<Personnel> GetPersonnelByIdAsync(int personnelId)
        {
            return await _context.Personnel.FindAsync(personnelId);
        }

        public async Task<Personnel> GetPersonnelByNumberAsync(string personnelNumber)
        {
            return await _context.Personnel
                .FirstOrDefaultAsync(p => p.personnel_number == personnelNumber);
        }

        public async Task<Personnel> CreatePersonnelAsync(Personnel personnel)
        {
            _context.Personnel.Add(personnel);
            await _context.SaveChangesAsync();
            return personnel;
        }

        // Employee methods
        public async Task<IEnumerable<EmployeeByDepartment>> GetEmployeesByDepartmentAsync(int departmentId)
        {
            return await _context.EmployeesByDepartment
                .Where(e => e.department_id == departmentId)
                .Include(e => e.Personnel)
                .ToListAsync();
        }

        public async Task<EmployeeByDepartment> CreateEmployeeAsync(EmployeeByDepartment employee)
        {
            _context.EmployeesByDepartment.Add(employee);
            await _context.SaveChangesAsync();
            return employee;
        }

        // Instruction methods
        public async Task<IEnumerable<Instruction>> GetInstructionsByDepartmentAsync(int departmentId)
        {
            return await _context.Instructions
                .Where(i => i.department_id == departmentId)
                .Include(i => i.InstructionType)
                .Include(i => i.FilePaths)
                .ToListAsync();
        }

        public async Task<Instruction> GetInstructionByIdAsync(int instructionId)
        {
            return await _context.Instructions
                .Include(i => i.InstructionType)
                .Include(i => i.FilePaths)
                .FirstOrDefaultAsync(i => i.instruction_id == instructionId);
        }

        public async Task<Instruction> CreateInstructionAsync(Instruction instruction)
        {
            _context.Instructions.Add(instruction);
            await _context.SaveChangesAsync();
            return instruction;
        }

        public async Task<bool> MarkInstructionAsPassedAsync(int instructionId, int personnelId)
        {
            var status = await _context.InstructionStatuses
                .FirstOrDefaultAsync(s => s.instruction_id == instructionId && s.personnel_id == personnelId);

            if (status == null)
                return false;

            status.is_instruction_passed = true;
            status.date_when_passed = DateTime.Now;
            status.date_when_passed_UTC = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Check if all personnel have passed this instruction
            var totalPersonnel = await _context.InstructionStatuses
                .CountAsync(s => s.instruction_id == instructionId);

            var passedPersonnel = await _context.InstructionStatuses
                .CountAsync(s => s.instruction_id == instructionId && s.is_instruction_passed);

            if (totalPersonnel == passedPersonnel)
            {
                var instruction = await _context.Instructions.FindAsync(instructionId);
                if (instruction != null)
                {
                    instruction.is_passed_by_everyone = true;
                    await _context.SaveChangesAsync();
                }
            }

            return true;
        }

        // User methods
        public async Task<User> GetUserByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Personnel)
                .FirstOrDefaultAsync(u => u.username == username);
        }

        public async Task<User> CreateUserAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return user;
        }
        public async Task<string> GetEmployeeFullNameAsync(int personnelId, int departmentId)
        {
            try
            {
                // First check if valid IDs were provided
                if (personnelId <= 0 || departmentId <= 0)
                {
                    return string.Empty;
                }

                // Fetch the employee record
                var employee = await _context.EmployeesByDepartment
                    .FirstOrDefaultAsync(e => e.personnel_id == personnelId && e.department_id == departmentId);

                // Return empty string if employee not found or full_name is null
                return employee?.full_name ?? string.Empty;
            }
            catch (Exception ex)
            {
                // Log the exception (if you have a logger)
                //_logger.LogError(ex, $"Error getting employee name for personnel ID {personnelId}, department ID {departmentId}");

                // Return empty string instead of throwing exception
                return string.Empty;
            }
        }
    }
}
