using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office2016.Drawing.Command;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Path = System.IO.Path;
using Newtonsoft.Json;
using Kotova.CommonClasses;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Security.Claims;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using System.Runtime.ConstrainedExecution;
using Microsoft.Extensions.Configuration;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using System.Data.Common;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Tracing;
using System.Text.Json;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Net;
using Microsoft.Data.SqlClient;
using DocumentFormat.OpenXml.Bibliography;
using System.Data;
using System.Timers;
using System.Transactions;
using System.Data.SqlClient;
using Department = Kotova.CommonClasses.Department;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Globalization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Xml.Schema;
using System.Linq;
using DocumentFormat.OpenXml.Office2010.Drawing.Charts;
using Microsoft.AspNetCore.Identity;

//Movig to schema in databases after that

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InstructionsController : ControllerBase
    {

        public const double DEVIATION = 0.00001;
        public const double maxFileSizeForExcel = 10 * 1024 * 1024; // Maximum file excel size (10 MB).
        public const string tableName_sql_index = "index";
        public const string tableName_sql_names = "full_name";
        public const string tableName_sql_jobPosition = "job_position";
        public const string tableName_sql_isDriver = "is_driver";
        public const string tableName_sql_BirthDate = "birth_date";
        public const string tableName_sql_gender = "gender";
        public const string tableName_sql_PN = "personnel_number";
        public const string tableName_sql_department = "department";
        public const string tableName_sql_departmentId = "department_id";
        public const string tableName_sql_isChiefOnline = "is_chief_online";
        public const string tableName_sql_lastOnlineSetUTC = "last_online_set_UTC";
        public const string tableName_sql_group = "group";
        public const string tableName_sql_departments_NameDB = "dbo.departments";
        public const string tableName_sql_MainName = "dbo.Department_employees";
        public const string tableName_Instructions_sql = "dbo.Instructions";
        public const string connectionString_server = "localhost";
        public const string connectionString_database = "TestDB";
        public const string tableName_pos_users = "users";
        public const string columnName_sql_pos_users_username = "username";
        public const string columnName_sql_pos_users_PN = "current_personnel_number";
        public const string tableName_sql_User_is_assigned_to_people = "is_assigned_to_people";
        public const string tableName_sql_USER_instruction_id = "instruction_id";
        public const string tableName_sql_USER_instruction_type = "type_of_instruction";
        public const string tableName_sql_USER_is_instruction_passed = "is_instruction_passed";
        public const string tableName_sql_USER_datePassed = "date_when_passed";
        public const string tableName_sql_User_datePassed_UTCTime = "date_when_passed_UTC_Time";
        public const string tableName_sql_INSTRUCTIONS_cause = "cause_of_instruction";
        public const string tableName_sql_USER_whenWasSendByHeadOfDepartment = "when_was_send_to_user";
        public const string tableName_sql_USER_whenWasSendByHeadOfDepartment_UTCTime = "when_was_send_to_user_UTC_Time";
        public const string tableName_sql_USER_instr_was_signed_by_PN = "was_signed_by_PN";
        public const string birthDate_format = "yyyy-MM-dd";




        private readonly MyDataService _dataService;
        private readonly ApplicationDBContextGeneralConstr _contextGeneralConstr;
        private readonly ApplicationDbContextUsers _userContext;
        private readonly ApplicationDBContextTechnicalDepartment _contextTechnicalDepartment;
        private readonly ApplicationDBContextManagement _contextManagement;

        public InstructionsController(MyDataService dataService, ApplicationDBContextGeneralConstr contextGeneralConstr, ApplicationDbContextUsers userContext, ApplicationDBContextTechnicalDepartment contextTechnicalDepartment, ApplicationDBContextManagement contextManagement)
        {
            _dataService = dataService;
            _contextGeneralConstr = contextGeneralConstr;
            _userContext = userContext;
            _contextTechnicalDepartment = contextTechnicalDepartment;
            _contextManagement = contextManagement;
        }


        [Authorize]
        [HttpGet("greeting")] //ИСПРАВЛЕНО
        public async Task<IActionResult> GetGreeting()
        {
            return Ok("Привет, мир!");
        }

        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        [HttpGet("get-department-id-by/{userName}")]  //ИСПРАВЛЕНО
        public async Task<IActionResult> GetDepartmentIdByUserNameURL(string userName)
        {
            var departmentId = await _userContext.Users
                   .Where(u => u.username == userName)
                   .Select(u => u.department_id)
                   .FirstOrDefaultAsync();
            if (departmentId == null)
            {
                return BadRequest("Номер отдела не найден по имю пользователя!");
            }
            return Ok(departmentId);
        }

        [Authorize]
        [HttpGet("get_instructions_for_user")]  //ИСПРАВЛЕНО, ВРОДЕ РАБОТАЕТ?
        public async Task<IActionResult> GetNotifications()
        {
            string? userName = User.FindFirst(ClaimTypes.Name)?.Value;
            string? tableNameForUser = await _userContext.Users
                .Where(u => u.username == userName)
                .Select(u => u.current_personnel_number)
                .FirstOrDefaultAsync();
            if (tableNameForUser == null) { return BadRequest($"Не найден персоналный номер для данного пользователя. Подождите пока появится персональный номер"); }
            int? departmentId = await _userContext.Users
                   .Where(u => u.username == userName)
                   .Select(u => u.department_id)
                   .FirstOrDefaultAsync();
            if (departmentId == null) { return BadRequest($"Номер отдела не найден!"); }
            int departmentIdNotNull = (int)departmentId;
            object whatever = await ReadDataFromDynamicTable(tableNameForUser, departmentIdNotNull);
            string serialized = JsonConvert.SerializeObject(whatever);
            string encryptedData = Encryption_Kotova.EncryptString(serialized);
            return Ok(encryptedData);
        }

        private async Task<object> ReadDataFromDynamicTable(string tableName, int departmentId)
        {
            var context = GetDbContextForDepartmentId(departmentId);
            if (context == null)
            {
                throw new ArgumentException("Invalid departmentId");
            }

            if (!Regex.IsMatch(tableName, @"^\d{10}$")) // Ensure the tableName is a valid 10-digit number
            {
                throw new ArgumentException("Invalid table name");
            }
            string schemaName = GetSchemaName(departmentId);

            // Step 1: Retrieve matching instruction IDs and `when_was_send_to_user` from the dynamic table
            var dynamicTableQuery = $"SELECT * FROM [{schemaName}].[{tableName}] WHERE is_instruction_passed = 0";
            var dynamicInstructions = await context.Set<DynamicEmployeeInstruction>()
                .FromSqlRaw(dynamicTableQuery)
                .ToListAsync();

            var instructionIds = dynamicInstructions.Select(di => di.instruction_id).ToList();
            var whenWasSentMap = dynamicInstructions.ToDictionary(di => di.instruction_id, di => di.when_was_send_to_user);

            // Step 2: Fetch instructions using these IDs
            var result1 = await context.Instructions
                .Where(i => instructionIds.Contains(i.instruction_id))
                .Select(i => new Dictionary<string, object>
                {
            { "ID", i.instruction_id },
            { "instruction_id", i.instruction_id },
            { "when_was_send_to_user", whenWasSentMap.ContainsKey(i.instruction_id) ? whenWasSentMap[i.instruction_id]: null},
            { "path_to_instruction", i.path_to_instruction },
            { "cause_of_instruction", i.cause_of_instruction },
            { "type_of_instruction", i.type_of_instruction }
                })
                .ToListAsync();

            var result2 = await context.FilePaths
                .Where(fp => instructionIds.Contains(fp.instruction_id))
                .Select(fp => new Dictionary<string, object>
                {
            { "instruction_id", fp.instruction_id },
            { "file_path", fp.file_path ?? null}
                })
                .ToListAsync();

            return new QueryResult
            {
                Result1 = result1,
                Result2 = result2
            };
        }
        private string GetSchemaName(int departmentId)
        {
            return departmentId switch
            {
                1 => "GeneralConstructionDep",
                2 => "TechnicalDep",
                5 => "Management",
                _ => "dbo" // Default schema
            };
        }


        [Authorize]
        [HttpPost("instruction_is_passed_by_user")] //ПРОВЕРЕНО, ТОЧНО РАБОТАЕТ НА ВВОДНЫХ ИНСТРУКТАЖАХ!
        public async Task<IActionResult> SendInstructionIsPassedToDB([FromBody] Dictionary<string, object> jsonDictionary)
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(jsonDictionary);

            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return BadRequest("Dictionary is empty or null on server side");
            }
            Dictionary<string, object> dictionaryOfInstruction = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);

            if (dictionaryOfInstruction.IsNullOrEmpty())
            {
                return BadRequest("Dictionary is empty or null on server side");
            }

            string? userName = User.FindFirst(ClaimTypes.Name)?.Value;
            string? personnelNumberForUser = await _userContext.Users
                .Where(u => userName == u.username)
                .Select(u => u.current_personnel_number)
                .FirstOrDefaultAsync();
            int departmentId = await GetDepartmentIdFromUserName(userName);
            if (personnelNumberForUser == null) { return BadRequest($"The personelNumber for this user isn't found. Wait till you have personel number"); }

            if (await PassInstructionIntoDb(dictionaryOfInstruction, personnelNumberForUser, departmentId))
            {
                return Ok("Instruction Is passed, information added to Database");
            }
            else
            {
                return NotFound("Instruction was not found in DB!");
            }
        }
        private async Task<bool> PassInstructionIntoDb(Dictionary<string, object> jsonDictionary, string personnelNumberForUser, int departmentId)
        {
            var context = GetDbContextForDepartmentId(departmentId); // Replace this with your actual method to get the context based on departmentId
            if (context == null)
            {
                throw new ArgumentException("Invalid departmentId");
            }

            string schemaName = GetSchemaName(departmentId);

            string instructionCause = ConvertJsonElement(jsonDictionary[DBProcessor.tableName_sql_INSTRUCTIONS_cause])?.ToString();

            if (string.IsNullOrEmpty(instructionCause))
            {
                throw new ArgumentException("Instruction cause is invalid or missing");
            }

            using (var transaction = await context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Step 1: Fetch the instruction based on the cause
                    var instruction = await context.Instructions
                        .Where(i => i.cause_of_instruction == instructionCause)
                        .Select(i => new
                        {
                            i.instruction_id,
                            i.type_of_instruction
                        })
                        .FirstOrDefaultAsync();

                    if (instruction == null)
                    {
                        Console.WriteLine("No rows matched the criteria.");
                        await transaction.RollbackAsync();
                        return false;
                    }

                    if (instruction.type_of_instruction == 0)
                    {
                        
                    }

                    // Step 2: Update the user's instruction record
                    var dynamicTableQuery = $"UPDATE [{schemaName}].[{personnelNumberForUser}] SET " +
                                            $"{DBProcessor.tableName_sql_USER_is_instruction_passed} = 1, " +
                                            $"{DBProcessor.tableName_sql_User_datePassed_UTCTime} = GETUTCDATE(), " +
                                            $"{DBProcessor.tableName_sql_USER_datePassed} = GETDATE() " +
                                            $"WHERE {DBProcessor.tableName_sql_USER_instruction_id} = @instructionId";

                    var rowsAffected = await context.Database.ExecuteSqlRawAsync(dynamicTableQuery,
                        new Microsoft.Data.SqlClient.SqlParameter("@instructionId", instruction.instruction_id));

                    if (rowsAffected > 0)
                    {
                        await transaction.CommitAsync();
                        return true;
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    await transaction.RollbackAsync();
                    return false;
                }
            }
        }


        private static object ConvertJsonElement(object value)
        {
            if (value is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetDecimal(),
                    JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
                    JsonValueKind.Undefined or JsonValueKind.Null => DBNull.Value,
                    _ => throw new ArgumentException("Unsupported JSON value kind."),
                };
            }
            return value;
        }

        


        [HttpPost("upload")] //ПОКА НЕ АКТУАЛЬНО!
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> UploadExcelFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Please upload a file.");

            if (file.Length > DBProcessor.maxFileSizeForExcel)
            {
                return BadRequest("Uploaded excel file size exceeds limit.");
            }

            string[] permittedMimeTypes = new string[]
            {
                "application/vnd.ms-excel",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
            var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
            var extensionToLower = Path.GetExtension(file.FileName).ToLowerInvariant();

            bool isPermittedMimeType = permittedMimeTypes.Contains(file.ContentType);
            bool isPermittedExtension = extensionToLower == ".xls" || extensionToLower == ".xlsx";

            if (!isPermittedMimeType || !isPermittedExtension)
            {
                return BadRequest("Only Excel files are allowed.");
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new ClosedXML.Excel.XLWorkbook(stream))
                    {
                        bool hasData = workbook.Worksheets.Any(ws => ws.RangeUsed() != null);
                        if (!hasData)
                        {
                            return BadRequest("The Excel file is empty.");
                        }
                    }
                }
            }
            catch (Exception)
            {
                return BadRequest("The file is not a valid Excel file.");
            }

            try
            {
                var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");

                if (directoryPath is null)
                {
                    throw new Exception("directory for UploadedFiles is empty");
                }
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                int index = DBProcessor.DetermineNextFileIndex(directoryPath);

                string newFileName = $"StandardName_{index}{extensionToLower}";
                string fullPath = Path.Combine(directoryPath, newFileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Ok(new { FileName = file.FileName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("download-newest")] //ПОКА НЕ АКТУАЛЬНО!
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public IActionResult DownloadNewestFile()
        {
            try
            {
                Response.Headers.Add("X-Content-Type-Options", "nosniff");
                string filePath = GetNewestExcelFilePath();
                string mimeType = Path.GetExtension(filePath).ToLowerInvariant() switch
                {
                    ".xls" => "application/vnd.ms-excel",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    _ => throw new InvalidOperationException("Unsupported file type.")
                };

                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(fileStream, mimeType, Path.GetFileName(filePath));
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        [HttpGet("sync-names-with-db")] //ИСПРАВЛЕНО, ВРОДЕ ВСЁ РАБОТАЕТ!
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> SyncNamesWithDB([FromServices] IConfiguration configuration)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                string? PNOfCurrentUser = await _userContext.Users
                    .Where(u => u.username == username)
                    .Select(u => u.current_personnel_number)
                    .FirstOrDefaultAsync();


                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim of Chief not found.");
                }

                int departmentId = await GetDepartmentIdFromUserName(username);


                ApplicationDBContextBase? dbContext = GetDbContextForDepartmentId(departmentId);
                if (dbContext == null)
                {
                    return BadRequest("Not Implemented case in function AddNewInstructionIntoDB, check for error there");
                }

                List<Tuple<string, string>> namesAndBirthDates = await dbContext.Department_employees
                    .Where(e => e.job_position != "Начальник отдела" && e.personnel_number != PNOfCurrentUser)
                     .Select(e => new Tuple<string, string>(e.full_name, e.birth_date.ToString("yyyy-MM-dd")))
                     .ToListAsync();
                return Ok(Encryption_Kotova.EncryptListOfTuples(namesAndBirthDates));
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }


        [HttpGet("sync-names-non-chief-with-db")] //ИСПРАВЛЕНО, ВРОДЕ ВСЁ РАБОТАЕТ!
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> SyncNamesOnlyForNonChiefUsersWithDB([FromServices] IConfiguration configuration)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                string? PNOfCurrentUser = await _userContext.Users
                    .Where(u => u.username == username)
                    .Select(u => u.current_personnel_number)
                    .FirstOrDefaultAsync();


                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim of Chief not found.");
                }

                int departmentId = await GetDepartmentIdFromUserName(username);


                ApplicationDBContextBase? dbContext = GetDbContextForDepartmentId(departmentId);
                if (dbContext == null)
                {
                    return BadRequest("Not Implemented case in function AddNewInstructionIntoDB, check for error there");
                }

                List<Tuple<string, string>> namesAndBirthDates = await dbContext.Department_employees
                    .Where(e => e.job_position != "Зам. начальника отдела" && e.job_position != "Начальник отдела" && e.personnel_number != PNOfCurrentUser)
                     .Select(e => new Tuple<string, string>(e.full_name, e.birth_date.ToString("yyyy-MM-dd")))
                     .ToListAsync();
                return Ok(Encryption_Kotova.EncryptListOfTuples(namesAndBirthDates));
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        private ApplicationDBContextBase GetDbContextForDepartmentId(int departmentId)
        {
            return departmentId switch
            {
                1 => _contextGeneralConstr,
                2 => _contextTechnicalDepartment,
                5 => _contextManagement,
                _ => null
            };
        }

        [HttpGet("sync-instructions-with-db")] //РАБОТАЕТ, ВРОДЕ, ВСЁ ОК!
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> SyncInstructionsWithDB()
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Пользователь с вашим именем не найден.");
                }
                List<Instruction> instructions = new List<Instruction>();
                int departmentId = await GetDepartmentIdFromUserName(username);
                var dbContext = GetDbContextForDepartmentId(departmentId);
                if (dbContext == null)
                {
                    return BadRequest("Not Implemented case in function AddNewInstructionIntoDB, check for error there");
                }
                instructions = await dbContext.Instructions
                    .Where(i => i.is_assigned_to_people == false && i.type_of_instruction == 1)
                    .ToListAsync();
                var serialized = JsonConvert.SerializeObject(instructions);
                var encryptedData = Encryption_Kotova.EncryptString(serialized);
                return Ok(encryptedData);

            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        [HttpPost("send-instruction-to-names")] //ПРОВЕРЕНО, РАБОТАЕТ
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> SendInstructionToNames([FromBody] InstructionPackage package)
        {
            
            return await SendInstructionToNamesInternal(package);
        }

        private async Task<IActionResult> SendInstructionToNamesInternal(InstructionPackage package)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            string? personnelNumberOfSignedBy = await _userContext.Users
                .Where(u => u.username == username)
                .Select(u => u.current_personnel_number)
                .FirstOrDefaultAsync();
            int departmentId = await _userContext.Users
                   .Where(u => u.username == username)
                   .Select(u => u.department_id)
                   .FirstOrDefaultAsync();
            ApplicationDBContextBase dbContext = GetDbContextForDepartmentId(departmentId);

            if (dbContext == null) { return BadRequest("Отдел для данного человека не найден! send-instruction-to-names failed."); }

            if (package == null)
            {
                return BadRequest("пустой package недопустим.");
            }
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("username claim of Chief(or User when used by Coordinator) не найден.");
                }

                bool result = await ProcessDataAsync(package, personnelNumberOfSignedBy, dbContext);
                if (result)
                {
                    return Ok($"Received and processed successfully: {package.InstructionCause} instructions with {package.NamesAndBirthDates.Count} names.");
                }
                else
                {
                    return BadRequest("Oops, something went wrong in ProcessDataAsync :(");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return BadRequest($"An error occurred while processing the instruction and names: {ex.Message}");
            }
        }

        private async Task<bool> ProcessDataAsync(InstructionPackage package, string PNOfSignedBy, ApplicationDBContextBase dbContext)
        {
            try
            {
                using (var transaction = await dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {

                        var instruction = await dbContext.Instructions
                        .FirstOrDefaultAsync(i => i.cause_of_instruction == package.InstructionCause); //ЗДЕСЬ ПРОВЕРКА ПО cause А НЕ ПО ID. ТАК СОЙДЁТ? ИЛИ ЛУЧШЕ ПО ID? ПОДУМАЙ.

                        if (instruction == null)
                        {
                            Console.WriteLine("Couldn't find instruction by its cause");
                            await transaction.RollbackAsync();
                            return false;
                        }

                        // Step 2: Set is_assigned_to_people to true
                        instruction.is_assigned_to_people = true;
                        dbContext.Instructions.Update(instruction);
                        await dbContext.SaveChangesAsync();

                        // Step 3: Find personnel numbers based on names and birthdates
                        var personnelNumbers = await FindPNsOfNamesAndBirthDates(package.NamesAndBirthDates, dbContext);

                        if (!personnelNumbers.Any())
                        {
                            Console.WriteLine("Couldn't find personnel numbers by full names");
                            await transaction.RollbackAsync();
                            return false;
                        }

                        // Step 4: Send notification to people
                        var isEverythingFine = await SendInstructionToPeopleAsync(personnelNumbers, instruction.instruction_id, dbContext, PNOfSignedBy);

                        if (!isEverythingFine)
                        {
                            Console.WriteLine("Couldn't add instruction to names");
                            await transaction.RollbackAsync();
                            return false;
                        }

                        await transaction.CommitAsync();
                        return true;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        private async Task<List<string>> FindPNsOfNamesAndBirthDates(List<Tuple<string, string>>? namesAndBirthDatesString, ApplicationDBContextBase context)
        {
            if (namesAndBirthDatesString == null || !namesAndBirthDatesString.Any())
                throw new ArgumentException("namesAndBirthDatesString is empty!");

            var (names, birthDates) = DeconstructNamesAndBirthDates(namesAndBirthDatesString);

            var personnelNumbers = await context.Department_employees
                .Where(e => names.Contains(e.full_name) && birthDates.Contains(e.birth_date))
                .Select(e => e.personnel_number)
                .ToListAsync();

            return personnelNumbers;
        }

        private (List<string>, List<DateTime>) DeconstructNamesAndBirthDates(List<Tuple<string, string>> namesAndBirthDatesString)
        {
            List<string> names = new List<string>();
            List<DateTime> birthDates = new List<DateTime>();

            foreach (var item in namesAndBirthDatesString)
            {
                try
                {
                    DateTime date = DateTime.ParseExact(item.Item2, birthDate_format, CultureInfo.InvariantCulture);
                    names.Add(item.Item1);
                    birthDates.Add(date);
                }
                catch (FormatException ex)
                {
                    Console.WriteLine($"Invalid date format for: {item.Item1} with date {item.Item2}");
                    throw;
                }
            }

            return (names, birthDates);
        }

        private async Task<bool> SendInstructionToPeopleAsync(List<string> personnelNumbers, int instructionId, ApplicationDBContextBase context, string personnelNumberOfSignedBy)
        {
            try
            {
                var userName = User.FindFirst(ClaimTypes.Name)?.Value;
                var schemaName = await UserNameToSchemaName(userName);
                if (schemaName == null ) { return false; }
                foreach (string personnelNumber in personnelNumbers)
                {
                    var userRole = await _userContext.Users.Where(u => u.current_personnel_number == personnelNumber)
                        .Select(u => u.user_role).FirstOrDefaultAsync();
                    var instructionType = await context.Instructions.Where(i => i.instruction_id == instructionId)
                        .Select(i => i.type_of_instruction).FirstOrDefaultAsync();
                    if (userRole == 2 && instructionType == 1) // Внеплановый инструктаж и отправлен заместителю начальинка отдела => заменяем подписанта - начальника, а не С.С.Кукушкина.
                    {
                        string tableNameTemp = $"[{schemaName}].[{personnelNumber}]";
                        string sql = $"UPDATE {tableNameTemp} SET was_signed_by_PN = @pnTemp WHERE instruction_id = @instructionId";

                        // Execute the raw SQL query with the parameter
                        context.Database.ExecuteSqlRaw(sql, new Microsoft.Data.SqlClient.SqlParameter("@pnTemp", personnelNumberOfSignedBy),
                            new Microsoft.Data.SqlClient.SqlParameter("@instructionId", instructionId));
                        continue;
                    }


                    string tableName = $"[{schemaName}].[{personnelNumber}]";
                    string query = $@"
                INSERT INTO {tableName} 
                ({DBProcessor.tableName_sql_USER_instruction_id}, 
                 {DBProcessor.tableName_sql_USER_is_instruction_passed}, 
                 {DBProcessor.tableName_sql_USER_whenWasSendByHeadOfDepartment}, 
                 {DBProcessor.tableName_sql_USER_whenWasSendByHeadOfDepartment_UTCTime}, 
                 {DBProcessor.tableName_sql_USER_instr_was_signed_by_PN}) 
                VALUES 
                (@instructionId, @falseValue, @whenWasSendToUser, @whenWasSendToUserUTC, @PNOfSignedBy)";

                    await context.Database.ExecuteSqlRawAsync(query,
                        new Microsoft.Data.SqlClient.SqlParameter("@instructionId", instructionId),
                        new Microsoft.Data.SqlClient.SqlParameter("@falseValue", false),
                        new Microsoft.Data.SqlClient.SqlParameter("@whenWasSendToUser", DateTime.Now),
                        new Microsoft.Data.SqlClient.SqlParameter("@whenWasSendToUserUTC", DateTime.UtcNow),
                        new Microsoft.Data.SqlClient.SqlParameter("@PNOfSignedBy", personnelNumberOfSignedBy));
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
        }

        [HttpGet("import-into-db")] //ПОКА НЕ АКТУАЛЬНО
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> ImportIntoDBAsync()
        {
            try
            {
                string excelFilePath = GetNewestExcelFilePath();
                DBProcessor example = new DBProcessor();
                await example.ImportDataFromExcelAsync(example.GetConnectionString(), excelFilePath);
                return Ok();
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        private string GetNewestExcelFilePath()
        {
            var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");
            if (!Directory.Exists(directoryPath))
            {
                throw new FileNotFoundException("Directory not found.");
            }

            var directoryInfo = new DirectoryInfo(directoryPath);
            var newestFile = directoryInfo.EnumerateFiles()
                              .Where(f => f.Extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
                                       || f.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                              .OrderByDescending(f => f.LastWriteTime)
                              .FirstOrDefault();

            if (newestFile == null)
            {
                throw new FileNotFoundException("No Excel files found in the directory.");
            }

            return newestFile.FullName;
        }


        [HttpPost("send-unplanned-instruction-to-chiefs")]
        [Authorize(Roles = "Management, Administrator")]
        public async Task<IActionResult> SubmitUnplannedInstruction([FromBody] UnplannedInstructionPackage package) 
        {
            try
            {
                if (package == null)
                {
                    return BadRequest("Пустые входные данные.");
                }

                List<string>? departmentsNames = package.DepartmentNames;

                if (departmentsNames == null)
                {
                    return BadRequest("Список отделов пусты на сервере");
                }
                foreach (var departmentName in departmentsNames)
                {
                    int? departmentId = await _userContext.Departments
                        .Where(d => d.department_name == departmentName)
                        .Select(d => d.department_id)
                        .FirstOrDefaultAsync();
                    if (departmentId == null)
                    {
                        return NotFound("Указанный отдел не найден, странно. Проверь что названия правильно указаны!");
                    }
                    int departmentIdNotNull = departmentId.Value;
                    var result = await AddNewInstructionInternal(package.FullInstruction, departmentIdNotNull);

                    if (!result.Success)
                    {
                        Console.WriteLine(result.ErrorMessage);
                        return BadRequest("Что-то пошло не так с добавлением инструкций в отдел. Обратитесь к администратору!");
                    }

                    if (departmentId == 5) //Это значит, что начальство
                    {
                        List<string> PNsofManagementOfDepartment = await _userContext.Users
                            .Where(u => u.department_id == departmentIdNotNull && u.user_role == 1) //Выбирем обычное руководтсво, не С.С.Кукушкина!
                            .Select(u => u.current_personnel_number)
                            .ToListAsync();
                        if (!PNsofManagementOfDepartment.Any())
                        {
                            Console.WriteLine($"Руководство в отделе: {departmentName} не найдены!");
                            continue;
                        }

                        FullCustomInstruction fullInstrForAssignmentForManagement = new FullCustomInstruction(result.Instruction, package.FullInstruction._paths);

                        foreach (string personnelNumber in PNsofManagementOfDepartment)
                        {
                            var result2 = await AssignNewInstructionToUser(fullInstrForAssignmentForManagement, departmentId, personnelNumber);
                            if (result2)
                            {
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("something went wrong in Submit unplanned instructions");
                                return BadRequest("Что-то пошло не так с назначением руководству!");
                            }
                        }

                        continue; //Просто skip для начальства, там не нужно назначать начальнику ничего!
                    }

                    FullCustomInstruction fullInstrForAssignment = new FullCustomInstruction(result.Instruction, package.FullInstruction._paths);

                    List<string?> PNofChiefsOfDepartment = await _userContext.Users
                        .Where(u => u.department_id == departmentIdNotNull && u.user_role == 2) // 2 равено роли начальника отдела (Chief of department)
                        .Select(u => u.current_personnel_number)
                        .ToListAsync();
                    if (!PNofChiefsOfDepartment.Any())
                    {
                        Console.WriteLine($"Начальники для данного отдела: {departmentName} не найдены!");
                        continue;
                    }
                    foreach (string personnelNumber in PNofChiefsOfDepartment)
                    {
                        var result2 = await AssignNewInstructionToUser(fullInstrForAssignment, departmentId, personnelNumber);
                        if (result2)
                        {
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("something went wrong in Submit unplanned instructions");
                            return BadRequest("Что-то пошло не так с назначением начальникам!");
                        }
                    }

                }
                

                return Ok("Инструктажи добавлены в отдел.");
            }
            catch (Exception ex)
            {
                // Log the error (not shown here for brevity)
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("add-new-instruction-into-db")] //ПРОВЕРЕНО.
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> AddNewInstructionIntoDB([FromBody] FullCustomInstruction fullInstruction)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim of Chief not found.");
                }
                int departmentId = await GetDepartmentIdFromUserName(username);
                var result = await AddNewInstructionInternal(fullInstruction, departmentId);

                if (result.Success)
                {
                    return Ok(result.Instruction);
                }
                else
                {
                    return BadRequest(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return BadRequest("Can't add instruction to DB. Most probably cause of instruction already exists in DB");
            }
        }

        private async Task<(bool Success, Instruction Instruction, string ErrorMessage)> AddNewInstructionInternal(FullCustomInstruction fullInstruction, int departmentId)
        {
            try
            {
                Instruction originalInstruction = fullInstruction._instruction;
                List<string> paths = fullInstruction._paths;


                // Create a new instruction for each department to avoid reusing the same instruction_id
                Instruction instruction = new Instruction
                {
                    instruction_id = originalInstruction.instruction_id,
                    cause_of_instruction = originalInstruction.cause_of_instruction,
                    begin_date = originalInstruction.begin_date,
                    end_date = originalInstruction.end_date,
                    path_to_instruction = originalInstruction.path_to_instruction,
                    type_of_instruction = originalInstruction.type_of_instruction,
                    is_passed_by_everyone = originalInstruction.is_passed_by_everyone,
                    is_assigned_to_people = originalInstruction.is_assigned_to_people
                };

                if (departmentId == 5)
                {
                    instruction.is_assigned_to_people = true;
                }


                List<FilePath> pathsOfFilePath = paths.Select(path => new FilePath
                {
                    file_path = path
                }).ToList();

                instruction.begin_date = DateTime.UtcNow;

                var dbContext = GetDbContextForDepartmentId(departmentId);
                if (dbContext == null)
                {
                    return (false, null, "Not Implemented case in function AddNewInstructionInternal, check for error there");
                }
                await SaveInstructionWithFilePaths(dbContext, instruction, pathsOfFilePath);

                return (true, instruction, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (false, null, "Can't add instruction to DB. Most probably cause of instruction already exists in DB");
            }
        }

        private async Task SaveInstructionWithFilePaths(ApplicationDBContextBase context, Instruction instruction, List<FilePath> pathsOfFilePath)
        {
            context.Instructions.Add(instruction);
            await context.SaveChangesAsync();

            foreach (var filePath in pathsOfFilePath)
            {
                filePath.instruction_id = instruction.instruction_id;
                context.FilePaths.Add(filePath);
            }

            await context.SaveChangesAsync();
        }

        private async Task<int> GetDepartmentIdFromUserName(string username)
        {
            var user = await _userContext.Users
            .Where(u => u.username == username)
            .Select(u => new { u.department_id })
            .FirstOrDefaultAsync();
            if (user == null)
            {
                return -1;
            }
            return user.department_id;
        }

        [HttpGet("download-list-of-all-departments-and-employees")] //ПРОВЕРЕНО
        [Authorize(Roles = "Management, Administrator")]
        public async Task<IActionResult> DownloadListOfDepartmentsAndEmployeesFromDB()
        {
            return await DownloadListOfDepartmentsAndEmployeesFromDBInternal();
        }

        private async Task<IActionResult> DownloadListOfDepartmentsAndEmployeesFromDBInternal()
        {
            try
            {
                var departments = await _userContext.Departments.ToListAsync();
                if (departments.Count == 0)
                {
                }
                List<Dept> deptList = new List<Dept>();
                foreach (var department in departments)
                {
                    using (var dbContext = GetDbContextForDepartmentId(department.department_id))
                    {
                        Dept new_dept = new Dept();
                        new_dept.Name = department.department_name;

                        new_dept.Employees = await dbContext.Department_employees
                                                            .ToListAsync();

                        deptList.Add(new_dept);
                    }
                }
                string deptListSerialized = JsonConvert.SerializeObject(deptList);
                return Ok(deptListSerialized);
            }
            catch
            {
                return StatusCode(500, "Internal Server Error: Could not retrieve departments.");
            }
        }

        [HttpGet("download-list-of-departments")] //ИСПРАВЛЕНО
        [Authorize(Roles = "Coordinator, Management, Administrator")]
        public async Task<IActionResult> DownloadListOfDepartmentsFromDB()
        {
            try
            {
                var departmentNames = await _userContext.Departments
                    .Select(dept => dept.department_name)
                    .ToListAsync();

                return Ok(departmentNames);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal Server Error: Could not retrieve departments.");
            }
        }

        [HttpGet("get-roles-for-newcomer")] //ИСПРАВЛЕНО
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> DownloadListOfRolesFromDB()
        {
            try
            {
                var roleTypes = await _userContext.Roles
                    .Select(role => role.roletype)
                    .ToListAsync();

                return Ok(roleTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal Server Error: Could not retrieve departments.");
            }
        }

        [HttpPost("insert-new-employee")] //ИСПРАВЛЕНО!
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> InsertNewcomerIntoDb([FromBody] Employee newcomer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ApplicationDBContextBase context;

            switch (newcomer.department)
            {
                case "Общестроительный отдел":
                    context = _contextGeneralConstr;
                    break;
                case "Технический отдел":
                    context = _contextTechnicalDepartment;
                    break;
                case "Руководство":
                    context = _contextManagement;
                    break;
                default:
                    return BadRequest("Недопустимый отдел!");
            }

            bool employeeExists = await context.Department_employees
                .AnyAsync(e => e.personnel_number == newcomer.personnel_number);
            string? schema = await _userContext.Departments
                .Where(dept => dept.department_name == newcomer.department)
                .Select(dept => dept.department_schema)
                .FirstOrDefaultAsync();
            if (schema == null) { return BadRequest("schema не найдена, проверьте данную строчку на сервере!"); }
            bool tablePNExists = await DoesTableExistAsync(context, newcomer.personnel_number, schema);

            if (employeeExists)
            {
                return BadRequest("Сотрудник с таким же табельным номером уже существует на предприятии");
            }
            if (tablePNExists)
            {
                return BadRequest("Сотрудник с таким же табельным номером уже существует в отделе.");
            }
            try
            {
                await DBProcessor.CreateTableDIAsync(newcomer.personnel_number, context, schema);
            }
            catch (Exception ex)
            {
                return BadRequest($"Таблица с таким табельным номером не может быть создана: {ex.Message}");
            }

            context.Department_employees.Add(newcomer);
            int rowsAffected = await context.SaveChangesAsync();

            if (rowsAffected > 0)
            {
                return Ok("Сотрудник добавлен в базу данных!");
            }
            return BadRequest("Что-то пошло не так, сотрудник не занесён в базу данных. Проверь InsertNewcomerIntoDb.");
        }






        private async Task<bool> DoesTableExistAsync<TContext>(TContext context, string tableName, string schemaName) where TContext : DbContext
        {
            var sqlQuery = "SELECT CASE WHEN EXISTS (" +
                           "SELECT * FROM INFORMATION_SCHEMA.TABLES " +
                           "WHERE TABLE_SCHEMA = @schemaName AND TABLE_NAME = @tableName) " +
                           "THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END";

            var parameters = new[]
            {
                new Microsoft.Data.SqlClient.SqlParameter("@SchemaName", schemaName),
                new Microsoft.Data.SqlClient.SqlParameter("@TableName", tableName)
            };

            var exists = await context.Database.ExecuteSqlRawAsync(sqlQuery, parameters);
            return exists == 1;
        }

        [HttpPost("get-login-and-password-for-newcommer")] //ПРОВЕРЕНО!
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> GenerateNewPasswordAndLogin([FromBody] List<string> someInfoAboutNewUser)
        {
            UserTemp newUser = new UserTemp(someInfoAboutNewUser[0], someInfoAboutNewUser[1], someInfoAboutNewUser[2], someInfoAboutNewUser[3], _userContext);

            bool isNeededToCreateInitialInstruction = someInfoAboutNewUser[4] == "True";

            try
            {
                if (newUser.UserRoleIndex is null)
                {
                    return BadRequest($"userRole {someInfoAboutNewUser[3]} is invalid to be put into Database, returning null");
                }

                var user = new User
                {
                    username = newUser.Login,
                    password_hash = newUser.HashedPassword,
                    user_role = newUser.UserRoleIndex.Value,
                    current_personnel_number = newUser.PersonnelNumber,
                    department_id = newUser.DepartmentId.Value,
                    desk_number = newUser.DeskNumber
                };

                _userContext.Users.Add(user);
                int result = await _userContext.SaveChangesAsync();

                if (result > 0)
                {
                    var whatever = new Tuple<string, string>(newUser.Login, newUser.Password);
                    string serialized = JsonConvert.SerializeObject(whatever);

                    if (isNeededToCreateInitialInstruction)
                    {
                        await FindNewEmployeeAndCreateInitialInstruction(newUser.PersonnelNumber, newUser.DepartmentId);
                    }

                    return Ok(serialized);
                }
                else
                {
                    Console.WriteLine("Error inserting user.");
                    return BadRequest($"Couldn't insert user: {newUser.Login} into database");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return BadRequest("Something went wrong while inserting user");
            }
        }

        private async Task FindNewEmployeeAndCreateInitialInstruction(string personnelNumber, int? departmentId)
        {
            try
            {
                var user = await _userContext.Users
                    .Where(u => u.current_personnel_number == personnelNumber && u.department_id == departmentId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    Console.WriteLine("User not found.");
                    return;
                }

                int userRole = user.user_role;

                var initialInstruction = new Instruction
                {
                    cause_of_instruction = $"Вводный инструктаж для {personnelNumber}",
                    begin_date = DateTime.UtcNow,
                    end_date = DateTime.UtcNow.AddMonths(1),
                    path_to_instruction = null,
                    is_assigned_to_people = true,
                    type_of_instruction = 0,
                };

                var fullCustomInstruction = new FullCustomInstruction
                {
                    _instruction = initialInstruction,
                    _paths = new List<string?> { null }
                };
                if (departmentId == null)
                {
                    Console.WriteLine("DepartmentID is null! in FindNewEmployeeAnd...");
                    return;
                }
                var departmentIdNotNull = departmentId.Value;
                var result = await AddNewInstructionInternal(fullCustomInstruction, departmentIdNotNull);

                if (!result.Success)
                {
                    Console.WriteLine(result.ErrorMessage);
                }
                else
                {
                    FullCustomInstruction newFullInstr = new FullCustomInstruction(result.Instruction, fullCustomInstruction._paths);
                    bool result2 = await AssignNewInstructionToUser(newFullInstr, departmentIdNotNull, personnelNumber);
                    if (result2)
                    {
                        if (userRole != 2 || user.department_id != 5) //Здесь мы режем, что НЕ начальство и НЕ руководитель отдела! TODO: Перевести в UsersSchema.roles = chiefofdepartment.
                        {
                            var result3 = await CreateTaskForChiefForPrimaryInstr(departmentIdNotNull, personnelNumber);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task<IActionResult> CreateTaskForChiefForPrimaryInstr(int departmentIdNotNull, string personnelNumber)
        {
            ApplicationDBContextBase dbContext = GetDbContextForDepartmentId(departmentIdNotNull);
            string? FullName = await dbContext.Department_employees
                .Where(u => u.personnel_number == personnelNumber)
                .Select(u => u.full_name)
                .FirstOrDefaultAsync();
            if (FullName == null)
            {
                throw new Exception("Не найден пользователь с таким персональным номером!");
            }

            DateTime dueDate = DateTime.Now.Date;

            string description = $"Создайте первичный инструктаж для человека под именем: {FullName} до {dueDate.ToString("dd-MM-yyyy")}";

            
            int userRole = 2; //Значит, что начальник отдела TODO: Можешь исправить, чтобы считывалось из базы данных. Но не сильно важно.

            

            // Create a new task for the user
            var newTask = new TaskForUser
            {
                Description = description,
                DepartmentId = departmentIdNotNull,
                UserRole = userRole,  
                AssignedTo = null,    // No specific user assigned
                CreatedAt = DateTime.Now,
                DueDate = dueDate,
                Status = "Назначено"
            };

            // Add the task to the database
            _userContext.Tasks.Add(newTask);
            await _userContext.SaveChangesAsync();

            return Ok();
        }

        private async Task<bool> AssignNewInstructionToUser(FullCustomInstruction fullCustomInstruction, int? departmentId, string personnelNumber)
        {
            // Get the username and personnel number of the person who signed the instruction
            string? userName = User.FindFirst(ClaimTypes.Name)?.Value;
            string? personnelNumberOfSignedBy = await _userContext.Users
                .Where(u => u.username == userName)
                .Select(u => u.current_personnel_number)
                .FirstOrDefaultAsync();

            if (departmentId == null) return false;
            int departmentIdNotNull = departmentId.Value;
            string schemaName = GetSchemaName(departmentIdNotNull);
            if (schemaName == "dbo") return false;

            ApplicationDBContextBase dbContext = GetDbContextForDepartmentId(departmentIdNotNull);
            if (dbContext == null) return false;

            try
            {
                var instructionId = fullCustomInstruction._instruction.instruction_id;
                List<string> personnelNumbers = new List<string> { personnelNumber };

                foreach (var personelNumber in personnelNumbers)
                {
                    string tableName = $"[{schemaName}].[{personelNumber}]";
                    string query = $@"
            INSERT INTO {tableName} 
            ({tableName_sql_USER_instruction_id}, 
            {tableName_sql_USER_is_instruction_passed}, 
            {tableName_sql_USER_whenWasSendByHeadOfDepartment}, 
            {tableName_sql_USER_whenWasSendByHeadOfDepartment_UTCTime}, 
            {tableName_sql_USER_instr_was_signed_by_PN}) 
            VALUES 
            (@instructionId, @falseValue, @whenWasSendToUser, @whenWasSendToUserUTC, @PNOfSignedBy)";

                    // Execute the query using the appropriate SQL parameters
                    await dbContext.Database.ExecuteSqlRawAsync(
                        query,
                        new Microsoft.Data.SqlClient.SqlParameter("@instructionId", instructionId),
                        new Microsoft.Data.SqlClient.SqlParameter("@falseValue", false),
                        new Microsoft.Data.SqlClient.SqlParameter("@whenWasSendToUser", DateTime.Now),
                        new Microsoft.Data.SqlClient.SqlParameter("@whenWasSendToUserUTC", DateTime.UtcNow),
                        new Microsoft.Data.SqlClient.SqlParameter("@PNOfSignedBy", personnelNumberOfSignedBy)
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        private async Task<string?> UserNameToSchemaName(string userName)
        {
            int? departmentId = await _userContext.Users
               .Where(u => u.username == userName)
               .Select(u => u.department_id)
               .FirstOrDefaultAsync();
            string? schemaName = await _userContext.Departments
                .Where(d => d.department_id == departmentId)
                .Select(d => d.department_schema)
                .FirstOrDefaultAsync();
            return schemaName;

        }



        [HttpGet("get-list-of-people-init-instructions")] //Сделано, но толку от простого отображения? Нужно чтобы можно было запросить с сервера 
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> GetNamesForInitialInstructions()
        {
            return await GetNamesForInitialInstructionsInternal();
        }

        private async Task<IActionResult> GetNamesForInitialInstructionsInternal()
        {
            List<ApplicationDBContextBase> dbContexts = DownloadListOfDBContextFromDB();
            List<InstructionDto> results = new List<InstructionDto>();
            List<string> errors = new List<string>();

            foreach (ApplicationDBContextBase dbContext in dbContexts)
            {
                var instructions = await dbContext.Instructions
                                                  .Where(i => EF.Functions.Like(i.cause_of_instruction, "Вводный инструктаж для %") && i.is_passed_by_everyone == false)
                                                  .ToListAsync();

                foreach (var instruction in instructions)
                {
                    string? personnelNumber = instruction.cause_of_instruction.ExtractTenDigitNumber();
                    if (string.IsNullOrEmpty(personnelNumber))
                    {
                        errors.Add($"Не удалось извлечь 10-значный номер из: {instruction.cause_of_instruction}");
                        continue;
                    }

                    var departmentId = await _userContext.Users
                        .Where(u => u.current_personnel_number == personnelNumber)
                        .Select(u => u.department_id)
                        .FirstOrDefaultAsync();
                    var schemaName = GetSchemaName(departmentId);

                    var dynamicTableQuery = $"SELECT TOP 1 is_instruction_passed FROM [{schemaName}].[{personnelNumber}]";
                    var isInstructionPassed = await dbContext.Set<DynamicEmployeeInstruction>()
                        .FromSqlRaw(dynamicTableQuery)
                        .Select(i => i.is_instruction_passed)
                        .FirstOrDefaultAsync();

                    if (isInstructionPassed == true)
                    {
                        continue;
                    }


                    var employee = await dbContext.Department_employees
                        .Where(e => e.personnel_number == personnelNumber)
                        .Select(e => new Employee
                        {
                            full_name = e.full_name,
                            birth_date = e.birth_date
                        })
                        .FirstOrDefaultAsync();
                    if (employee == null)
                    {
                        errors.Add($"Не найден сотрудник с персональным номером: {personnelNumber}");
                        continue;
                    }

                    results.Add(new InstructionDto
                    {
                        InstructionId = instruction.instruction_id,
                        TenDigitNumber = personnelNumber,
                        Name = employee.full_name,
                        BirthDate = employee.birth_date
                    });
                }
            }

            if (errors.Any())
            {
                return BadRequest(new { Errors = errors });
            }

            return Ok(results);
        }

        private List<ApplicationDBContextBase> DownloadListOfDBContextFromDB()
        {
            List<ApplicationDBContextBase> dbContexts = new List<ApplicationDBContextBase>
            {
                _contextGeneralConstr,
                _contextTechnicalDepartment
            };
            return dbContexts;
        }

        [HttpGet("get-not-passed-instructions-for-chief")] //ПРОВЕРЕНО, для Начальника вроде всё работает. Только для администратора не будет находить. Исправь это, если захочешь
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> GetNotPassedInstructionForChief()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized("username для данного начальника не найден");
            }

            int departmentId = await GetDepartmentIdFromUserName(username);

            var dbContext = GetDbContextForDepartmentId(departmentId);
            if (dbContext == null)
            {
                return NotFound("для данного начальника не найден отдел! (GetNotPassedInstructionForChief)");
            }

            return await CheckPassingTheInstructionsBeforeReturningTheData(dbContext, departmentId);
        }

        private async Task<IActionResult> CheckPassingTheInstructionsBeforeReturningTheData(ApplicationDBContextBase dbContext,int departmentId)
        {
            var instructionsToCheck = await dbContext.Instructions
                .Where(i => !i.is_passed_by_everyone)
                .ToListAsync();

            if (!instructionsToCheck.Any())
            {
                return Ok(JsonConvert.SerializeObject(new List<InstructionForChief>()));
            }

            var schemaName = await _userContext.Departments
                .Where(d => d.department_id == departmentId)
                .Select(d => d.department_schema)
                .FirstOrDefaultAsync();
            if (schemaName == null) { return BadRequest("schema не найдена для данного отдела (CheckPassingTheInstructionsBeforeReturningTheData)"); }

            try
            {
                var tenDigitTables = dbContext.GetTenDigitTableNames(schemaName);
                List<InstructionForChief> instructionsForChiefList = new List<InstructionForChief>();

                foreach (var instructionToCheck in instructionsToCheck)
                {
                    int instructionId = instructionToCheck.instruction_id;

                    var instructionIsNotPassedByListOfPeople = new List<(string personnelNumber, string personName)>();
                    var instructionIsPassedByListOfPeople = new List<(string personnelNumber, string personName)>();

                    foreach (string? tableName in tenDigitTables)
                    {
                        string[] parts = tableName.Split('.');
                        if (parts.Length != 2)
                        {
                            throw new ArgumentException("Не подходящий формат. Ожидаемый формат: 'SchemaName.TableName'");
                        }
                        string tenDigitName = parts[1];


                        if (string.IsNullOrEmpty(tableName))
                        {
                            Console.WriteLine("Table name is null or empty.");
                            continue;
                        }

                        // Checking if the instruction is not passed by the personnel
                        var notPassedQuery = dbContext.Set<DynamicEmployeeInstruction>()
                            .FromSqlRaw($"SELECT * FROM [{schemaName}].[{tenDigitName}] WHERE [instruction_id] = @instructionId AND is_instruction_passed = 0",
                            new Microsoft.Data.SqlClient.SqlParameter("@instructionId", instructionId))
                            .Count();

                        if (notPassedQuery > 0)
                        {
                            var personName = await dbContext.Department_employees
                                .Where(e => e.personnel_number == tenDigitName)
                                .Select(e => e.full_name)
                                .FirstOrDefaultAsync();

                            instructionIsNotPassedByListOfPeople.Add((tableName, personName));
                        }
                        else
                        {
                            var passedQuery = dbContext.Set<DynamicEmployeeInstruction>()
                                .FromSqlRaw($"SELECT * FROM [{schemaName}].[{tenDigitName}] WHERE [instruction_id] = @instructionId AND is_instruction_passed = 1",
                                new Microsoft.Data.SqlClient.SqlParameter("@instructionId", instructionId))
                                .Count();

                            if (passedQuery > 0)
                            {
                                var personName = await dbContext.Department_employees
                                    .Where(e => e.personnel_number == tenDigitName)
                                    .Select(e => e.full_name)
                                    .FirstOrDefaultAsync();

                                instructionIsPassedByListOfPeople.Add((tableName, personName));
                            }
                        }
                    }

                    if (!instructionIsNotPassedByListOfPeople.Any() && instructionIsPassedByListOfPeople.Any())
                    {
                        var instructionToUpdate = await dbContext.Instructions
                            .FirstOrDefaultAsync(i => i.instruction_id == instructionId);

                        if (instructionToUpdate != null)
                        {
                            instructionToUpdate.is_passed_by_everyone = true;
                            dbContext.Instructions.Update(instructionToUpdate);
                            await dbContext.SaveChangesAsync();
                            continue;
                        }
                    }

                    var persons = instructionIsNotPassedByListOfPeople.Select(p => new InstructionForChief.PersonStatus
                    {
                        PersonnelNumber = p.personnelNumber,
                        PersonName = p.personName,
                        Passed = false
                    }).Concat(instructionIsPassedByListOfPeople.Select(p => new InstructionForChief.PersonStatus
                    {
                        PersonnelNumber = p.personnelNumber,
                        PersonName = p.personName,
                        Passed = true
                    })).ToList();

                    InstructionForChief instructionForChief = new InstructionForChief()
                    {
                        InstructionId = instructionId,
                        BeginDate = instructionToCheck.begin_date,
                        EndDate = instructionToCheck.end_date,
                        CauseOfInstruction = instructionToCheck.cause_of_instruction,
                        TypeOfInstruction = instructionToCheck.cause_of_instruction,
                        Persons = persons,
                    };

                    instructionsForChiefList.Add(instructionForChief);
                }

                var instructionsForChiefList_Serialized = JsonConvert.SerializeObject(instructionsForChiefList);
                return Ok(instructionsForChiefList_Serialized);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                Console.WriteLine(ex);
                return BadRequest("Упс, что-то пошло не так в CheckPassingTheInstructionsByChief, проверь!");
            }
        }

        [HttpPost("instructions-data-export")]
        [Authorize(Roles = "ChiefOfDepartment, Coordinator, Manager, Administrator")]
        public async Task<IActionResult> InstructionsDataExport([FromBody] InstructionExportRequest instructionExportRequest)
        {

            var startDate = instructionExportRequest.StartDate;
            var endDate = instructionExportRequest.EndDate.AddDays(1);

            if (instructionExportRequest == null)
            {
                return BadRequest("instructionsDataExport - пуст, ошибка!");
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var userName = User.FindFirst(ClaimTypes.Name)?.Value;

            var PNofUser = await _userContext.Users
                .Where(u => u.username == userName)
                .Select(u => u.current_personnel_number)
                .FirstOrDefaultAsync();

            List<Byte> typesOfInstruction = instructionExportRequest.InstructionTypes;
            string typesOfInstructionString = string.Join(",", typesOfInstruction);

            var department_idString = User.FindFirst("department_id")?.Value;
            if (department_idString == null || userRole == null)
            {
                return BadRequest("Ваш отдел или роль не найдена!");
            }
            int department_id = Int32.Parse(department_idString);

            ApplicationDBContextBase departmentDbContext = GetDbContextForDepartmentId(department_id);

            var PNOfChief = await departmentDbContext.Department_employees //Выкидываем только начальника отдела!
                        .Where(e => e.job_position == "Начальник отдела")
                        .Select(e => e.personnel_number)
                        .FirstOrDefaultAsync();

            if (departmentDbContext == null)
            {
                return BadRequest("Не найден dbContext для данного user-а");
            }
            List<InstructionExportInstance> listOfInstructions = new List<InstructionExportInstance>();
            string schema = GetSchemaName(department_id);
            if (schema == null)
            {
                return BadRequest("Не найдена schema для данного user-а");
            }
            if (userRole == "User") // Если пользователь  - User, то выкидываем его :)
            {
                return Unauthorized("Как ты сюда попал, User? :)");
            }
            else if (userRole == "ChiefOfDepartment") 
            {
                List<string> PNtables = departmentDbContext.GetTenDigitTableNames(schema);

                
                foreach (string pnTable in PNtables)
                {
                    //if (pnTable.Split('.')[1] == PNofUser) //ПЛОХОЙ ВАРИАНТ ГДЕ МЫ УБИРЕМ ТОЛЬКО САМОГО СОТРУДНИКА.
                    if (pnTable.Split('.')[1] == PNOfChief)
                    {
                        continue;
                    }
                    string schemaName = pnTable.Split('.')[0];
                    string tenDigitName = pnTable.Split('.')[1];
                    var personnelNumberParam = new Microsoft.Data.SqlClient.SqlParameter("@tenDigitName", tenDigitName);


                    List<InstructionExportInstance> justSomeInstructions = await departmentDbContext.Set<InstructionExportInstance>()
                     .FromSqlRaw($@"
                        SELECT 
                            ei.instruction_id AS InstructionId,
                            ei.date_when_passed AS DateWhenPassedByEmployee, 
                            de.full_name AS FullNameOfEmployee, 
                            de.job_position AS PositionOfEmployee, 
                            de.birth_date AS BirthDateOfEmployee, 
                            e.type_of_instruction AS InstructionType, 
                            e.cause_of_instruction AS CauseOfInstruction, 
                            ei.was_signed_by_PN AS FullNameOfEmployeeWhoConductedInstruction
                        FROM [{schemaName}].[{tenDigitName}] ei
                        JOIN [{schemaName}].Instructions e ON ei.instruction_id = e.instruction_id
                        JOIN [{schemaName}].Department_employees de ON de.personnel_number = {tenDigitName}
                        WHERE ei.is_instruction_passed = 1
                        AND e.type_of_instruction IN ({typesOfInstructionString})
                        AND ei.date_when_passed BETWEEN @startDate AND @endDate",
                         new Microsoft.Data.SqlClient.SqlParameter("@startDate", startDate),
                         new Microsoft.Data.SqlClient.SqlParameter("@endDate", endDate))
                     .ToListAsync(); //Вот эта sql выдала что никто не прошел, хотя 2 инструктажа пройдены. Проверяй! TODO: 



                    if (justSomeInstructions.IsNullOrEmpty()) { continue; }

                    var itemsToRemove = new List<InstructionExportInstance>();

                    foreach (var instance in justSomeInstructions)
                    {
                        var PNOfEmployeeWhoConductedInstruction = instance.FullNameOfEmployeeWhoConductedInstruction; //Its actually just currentPN of someone who conductedInstruction
                        var departmentIdOfEmployeeWhoConductedInstruction = await _userContext.Users.Where(u => u.current_personnel_number == PNOfEmployeeWhoConductedInstruction)
                            .Select(u => u.department_id).FirstOrDefaultAsync();

                        if (departmentIdOfEmployeeWhoConductedInstruction == 5)
                        {
                            itemsToRemove.Add(instance);
                            continue;
                        }

                        var dbContextOfWhoConductedInstruction = GetDbContextForDepartmentId(departmentIdOfEmployeeWhoConductedInstruction);
                        var employeeDetailsOfSomeoneWhoConductedInstruction = await dbContextOfWhoConductedInstruction.Department_employees.Where(d => d.personnel_number == PNOfEmployeeWhoConductedInstruction)
                            .Select(u => new
                            {
                                CombinedDetails = u.full_name + " - " + u.job_position
                            })
                            .FirstOrDefaultAsync();

                        instance.FullNameOfEmployeeWhoConductedInstruction = employeeDetailsOfSomeoneWhoConductedInstruction.CombinedDetails;



                        var filePaths = await departmentDbContext.FilePaths
                            .Where(fp => fp.instruction_id == instance.InstructionId)
                            .Select(fp => fp.file_path ?? string.Empty) 
                            .ToListAsync();

                        if (filePaths.Any())
                        {
                            if (instance.FileNamesOfInstruction == null)
                            {
                                instance.FileNamesOfInstruction = new List<string>();
                            }
                            instance.FileNamesOfInstruction.AddRange(filePaths);
                        }
                        else
                        {
                            instance.FileNamesOfInstruction = null;
                        }

                        string fileNames = ""; 
                        for (int i = 0; i < filePaths.Count; i++)
                        {
                            string[] filepathTemp = filePaths[i].Split("\\");
                            string fileName = filepathTemp[filepathTemp.Length - 1];
                            if (i == (filepathTemp.Length-1)) 
                            {
                                fileNames += fileName;
                                break;
                            }
                            fileNames += (fileName + " ");
                        }
                        instance.FileNamesOfInstructionInOneString = fileNames;

                    }
                    foreach (var item in itemsToRemove)
                    {
                        justSomeInstructions.Remove(item);
                    }

                    listOfInstructions.AddRange(justSomeInstructions);
                }
            }
            return Ok(listOfInstructions);
        }

        public class UserTemp
        {
            ApplicationDbContextUsers _userContext;
            public string PersonnelNumber { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
            public string HashedPassword { get; set; }
            public int? DepartmentId { get; set; }
            public string? DeskNumber { get; set; }
            public int? UserRoleIndex { get; set; }

            public UserTemp(string personnelNumber, string departmentName, string deskNumber, string userRole, ApplicationDbContextUsers userContext)
            {
                PersonnelNumber = personnelNumber;
                DepartmentId = departmentNameToId(departmentName);
                DeskNumber = deskNumber;
                _userContext = userContext;

                Random random = new Random();


                int randomNumber = random.Next(1000000, 9999999);
                Login = $"User{randomNumber}";
                while (userContext.Users.Where(r => r.username == Login).Any())
                {
                    randomNumber = random.Next(1000000, 9999999);
                    Login = $"User{randomNumber}";
                }
                Password = Login;
                HashedPassword = Encryption_Kotova.HashPassword(Password);
                UserRoleIndex = IndexFromUserRole(userRole);
            }

            private int? IndexFromUserRole(string userRole)
            {

                    var roleId = _userContext.Roles
                                          .Where(r => r.roletype == userRole)
                                          .Select(r => r.roleid)
                                          .FirstOrDefault();

                    List<string> validRoles = new List<string> { "user", "chief of department", "management" };
                    if (validRoles.Contains(userRole))
                    {
                        return roleId;
                    }
                    else
                    {
                        return null;
                    }
            }

            private int departmentNameToId(string departmentName)
            {
                return departmentName switch
                {
                    "Общестроительный отдел" => 1,
                    "Технический отдел" => 2,
                    "Руководство" => 5,
                    _ => -1
                };
            }

            public override string ToString()
            {
                return $"Personnel Number: {PersonnelNumber}, Login: {Login}, Password: {Password}";
            }
        }
        
    }
    


}


//THE CODE BELOW IS BEFORE ATTEMPTING TO CHANGE EVERYTHING BY CHATGPT.
/*using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office2016.Drawing.Command;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Path = System.IO.Path;
using Newtonsoft.Json;
using Kotova.CommonClasses;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Security.Claims;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using System.Runtime.ConstrainedExecution;
using Microsoft.Extensions.Configuration;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using System.Data.Common;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Tracing;
using System.Text.Json;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Net;
using Microsoft.Data.SqlClient;
using DocumentFormat.OpenXml.Bibliography;
using System.Data;
using System.Timers;
using System.Transactions;

using System.Data.SqlClient;
using Department = Kotova.CommonClasses.Department;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InstructionsController : ControllerBase
    {
        private readonly MyDataService _dataService;
        private readonly ApplicationDBContextGeneralConstr _contextGeneralConstr;
        private readonly ApplicationDbContextUsers _userContext;
        private readonly ApplicationDBContextTechnicalDepartment _contextTechnicalDepartment;
        private readonly ApplicationDBContextManagement _contextManagement;
        private List<bool> ChiefsAreOnline = new List<bool>();

        public InstructionsController(MyDataService dataService, ApplicationDBContextGeneralConstr contextGeneralConstr, ApplicationDbContextUsers userContext, ApplicationDBContextTechnicalDepartment contextTechnicalDepartment, ApplicationDBContextManagement contextManagement)
        {

            _dataService = dataService;
            _contextGeneralConstr = contextGeneralConstr;
            _userContext = userContext;
            _contextTechnicalDepartment = contextTechnicalDepartment;
            _contextManagement = contextManagement;

        }

        [Authorize]
        [HttpGet("greeting")]
        public IActionResult GetGreeting()
        {
            return Ok("Hello, World!");
        }

        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        [HttpGet("get-department-id-by/{userName}")]
        public async Task<IActionResult> GetDepartmentIdByUserNameURL(string userName) //Передвинь эту функцию в инструктажи, or something.
        {
            int? departmentId = await _dataService.GetDepartmentIdByUserName(userName);
            if (departmentId == null)
            {
                return BadRequest("departmentId wan't found by userName");
            }
            return Ok(departmentId);
        }

        [Authorize]
        [HttpGet("get_instructions_for_user")]
        public async Task<IActionResult> GetNotifications()
        {
            string? userName = User.FindFirst(ClaimTypes.Name)?.Value;
            string? tableNameForUser = await _dataService.UserNameToTableName(userName);
            if (tableNameForUser == null) { return BadRequest($"The personelNumber for this user wasn't found. Wait till you have personel number"); }
            int? departmentId = await _dataService.GetDepartmentIdByUserName(userName);
            if (departmentId == null) { return BadRequest($"The departmentId for this user wasn't found"); }
            object whatever = await _dataService.ReadDataFromDynamicTable(tableNameForUser, departmentId);
            string serialized = JsonConvert.SerializeObject(whatever);
            string encryptedData = Encryption_Kotova.EncryptString(serialized);
            return Ok(encryptedData);
        }


        [Authorize]
        [HttpPost("instruction_is_passed_by_user")] // Здесь нужна(или не нужна?) кодировка вместо Dictionary - string, которая зашифрована.!!!!!!!!
        public async Task<IActionResult> sendInstructionIsPassedToDB([FromBody] Dictionary<string, object> jsonDictionary)
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(jsonDictionary);

            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return BadRequest("Dictionary is empty or null on server side");
            }
            Dictionary<string, object> dictionaryOfInstruction = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString); //supress the warning.

            if (dictionaryOfInstruction.IsNullOrEmpty())
            {
                return BadRequest("Dictionary is empty or null on server side");
            }

            string? userName = User.FindFirst(ClaimTypes.Name)?.Value;
            string? tableNameForUser = await _dataService.UserNameToTableName(userName);
            int departmentId = await GetDepartmentIdFromUserName(userName);
            if (tableNameForUser == null) { return BadRequest($"The personelNumber for this user isn't found. Wait till you have personel number"); }

            if (await passInstructionIntoDb(dictionaryOfInstruction, tableNameForUser, departmentId))
            {
                return Ok("Instruction Is passed, information added to Database");
            }
            else
            {
                return NotFound("Instruction was not found in DB!");
            }
            
        }

        private async Task<bool> passInstructionIntoDb(Dictionary<string, object> jsonDictionary, string tableNameForUser, int departmentId)
        {
            string connectionString = GetConnectionStringByDepartmentId(departmentId);

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDBContextGeneralConstr>();
            optionsBuilder.UseSqlServer(connectionString);

            string tableName = DBProcessor.tableName_Instructions_sql;
            string columnName = DBProcessor.tableName_sql_INSTRUCTIONS_cause;

            string instructionIdColumn = DBProcessor.tableName_sql_USER_instruction_id; 
            string isPassedColumn = DBProcessor.tableName_sql_USER_is_instruction_passed; 

            string dateWhenPassed = DBProcessor.tableName_sql_USER_datePassed;
            string dateWhenPassedUTCTime = DBProcessor.tableName_sql_User_datePassed_UTCTime;
            string instructionTypeColumn = DBProcessor.tableName_sql_USER_instruction_type;

            string sqlQuery = @$"SELECT * FROM [{tableName.Split('.')[1]}] WHERE [{columnName}] = @value";
            string sqlQueryChangePassedVariable = @$"UPDATE [{tableNameForUser}]
            SET [{isPassedColumn}] = 1,
                [{dateWhenPassedUTCTime}] = GETUTCDATE(),
                [{dateWhenPassed}] = GETDATE()           
            WHERE [{instructionIdColumn}] = @instructionId";

            using (var context = new ApplicationDBContextGeneralConstr(optionsBuilder.Options))
            {
                var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (var command = conn.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = sqlQuery;
                            DbParameter param = command.CreateParameter();
                            param.ParameterName = "@value";
                            param.Value = ConvertJsonElement(jsonDictionary[columnName]);
                            command.Parameters.Add(param);

                            int rowCount = 0;
                            int instructionId = 0;
                            int typeOfInstruction = -1;

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    rowCount++;
                                    instructionId = reader.GetInt32(reader.GetOrdinal(instructionIdColumn));
                                    typeOfInstruction = reader.GetByte(reader.GetOrdinal(instructionTypeColumn));
                                }
                            }

                            if (rowCount == 1)
                            {
                                if (typeOfInstruction == 0)
                                {
                                    Console.WriteLine("NOT IMPLEMENTED! COMPLETE THE CODE PLEASE :)");//TODO: Отправить начальнику уведомление о создании вводного инструктажа для такого-то человека! Вводный инструктаж пройден
                                }
                                command.Parameters.Clear(); 

                                command.CommandText = sqlQueryChangePassedVariable;
                                DbParameter instructionIdParam = command.CreateParameter();
                                instructionIdParam.ParameterName = "@instructionId";
                                instructionIdParam.Value = instructionId;
                                command.Parameters.Add(instructionIdParam);

                                int rowsAffected = await command.ExecuteNonQueryAsync();
                                if (rowsAffected > 0)
                                {
                                    transaction.Commit();
                                    return true;
                                }
                                else
                                {
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                            else if (rowCount > 1)
                            {
                                throw new Exception("Instructions with the same cause name were found in multiple quantities!");
                            }
                            else
                            {
                                Console.WriteLine("No rows matched the criteria.");
                                transaction.Rollback();
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An error occurred: {ex.Message}");
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }


        private static object ConvertJsonElement(object value)
        {
            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        return element.GetString();
                    case JsonValueKind.Number:
                        return element.GetDecimal();
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return element.GetBoolean();
                    case JsonValueKind.Undefined:
                    case JsonValueKind.Null:
                        return DBNull.Value;
                    default:
                        throw new ArgumentException("Unsupported JSON value kind.");
                }
            }
            return value;
        }


        [HttpPost("upload")] //СДЕЛАТЬ ПРОВЕРКУ ПО РАЗМЕРУ ФАЙЛА EXCEL, по совету ментора
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> UploadExcelFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Please upload a file.");

            if (file.Length > DBProcessor.maxFileSizeForExcel)
            {
                return BadRequest("Uploaded excel file size exceeds limit.");
            }

            // Check the MIME type to see if it's an Excel file 
            string[] permittedMimeTypes = new string[]
            {
                "application/vnd.ms-excel",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
            var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
            var extensionToLower = Path.GetExtension(file.FileName).ToLowerInvariant();

            bool isPermittedMimeType = permittedMimeTypes.Contains(file.ContentType);
            bool isPermittedExtension = extensionToLower == ".xls" || extensionToLower == ".xlsx";

            if (!isPermittedMimeType || !isPermittedExtension)
            {
                return BadRequest("Only Excel files are allowed.");
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new ClosedXML.Excel.XLWorkbook(stream))
                    {
                        // Perform a basic check to check file integrity
                        bool hasData = workbook.Worksheets.Any(ws => ws.RangeUsed() != null);
                        if (!hasData)
                        {
                            return BadRequest("The Excel file is empty.");
                        }
                    }
                }
            }
            catch (Exception)
            {
                return BadRequest("The file is not a valid Excel file.");
            }

            try
            {
                var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");

                if (directoryPath is null)
                {
                    throw new Exception("directory for UploadedFiles is empty");
                }
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                int index = DBProcessor.DetermineNextFileIndex(directoryPath);

                // Construct the new file name
                string newFileName = $"StandardName_{index}{extensionToLower}";
                string fullPath = Path.Combine(directoryPath, newFileName);
                // Added: Path traversal check!!! turn on if directory path changes.
                *//*
                if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(directoryPath))) // Added: Path traversal check
                {
                    return BadRequest("Invalid file path entered. (Path traversal attack)");
                }
                *//*
                // Save the file
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Ok(new { FileName = file.FileName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("download-newest")]
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public IActionResult DownloadNewestFile()
        {
            try
            {
                Response.Headers.Add("X-Content-Type-Options", "nosniff"); // FOR SECURITY
                string filePath = GetNewestExcelFilePath();
                string mimeType = Path.GetExtension(filePath).ToLowerInvariant() switch
                {
                    ".xls" => "application/vnd.ms-excel",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    _ => throw new InvalidOperationException("Unsupported file type.")
                };

                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(fileStream, mimeType, Path.GetFileName(filePath));
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        [HttpGet("sync-names-with-db")]
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> SyncNamesWithDB([FromServices] IConfiguration configuration)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim of Chief not found.");
                }

                int departmentId = await GetDepartmentIdFromUserName(username);
                string? connectionString = GetConnectionStringByDepartmentId(departmentId);

                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Connection string is not configured for the provided department ID.");
                }

                DBProcessor dbProcessor = new DBProcessor(connectionString);
                List<Tuple<string, string>> namesAndBirthDates = dbProcessor.GetNames();
                return Ok(Encryption_Kotova.EncryptListOfTuples(namesAndBirthDates));
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        private string GetConnectionStringByDepartmentId(int departmentId)
        {
            switch (departmentId)
            {
                case 1:
                    return _dataService._configuration.GetConnectionString("DefaultConnectionForGeneralConstructionDepartment");
                case 2:
                    return _dataService._configuration.GetConnectionString("DefaultConnectionForTechnicalDepartment");
                case 5:
                    return _dataService._configuration.GetConnectionString("DefaultConnectionForManagement");
                default:
                    return null;
            }
        }

        private ApplicationDBContextBase GetDbContextForDepartment(int departmentId)
        {
            switch (departmentId)
            {
                case 1:
                    return _contextGeneralConstr;
                case 2:
                    return _contextTechnicalDepartment;
                case 5:
                    return _contextManagement;
                default:
                    return null;
            }
        }

        [HttpGet("sync-instructions-with-db")] //Make this load every 1 minute or something.!!!!!
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> SyncInstructionsWithDB()
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim of Chief not found.");
                }
                List<Instruction> instructions = new List<Instruction>();
                int departmentId = await GetDepartmentIdFromUserName(username);
                var dbContext = GetDbContextForDepartment(departmentId);
                if (dbContext == null) 
                {
                    return BadRequest("Not Implemented case in function AddNewInstructionIntoDB, check for error there");
                }
                instructions = await dbContext.Instructions.ToListAsync();  
                var serialized = JsonConvert.SerializeObject(instructions);
                var encryptedData = Encryption_Kotova.EncryptString(serialized);
                return Ok(encryptedData);

            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }
        [HttpPost("send-instruction-and-names")]
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> SendInstructionAndNames([FromBody] InstructionPackage package) // ITS FOR SENDING INSTRUCTIONS TO SELECTED PEOPLE? //REWRITE IT IN CASE OF USING ENCRYPTED STUFF(JSON - STRING!)
        //public async Task<IActionResult> ReceiveInstructionAndNamesAsync([FromBody] string encryptedData)
        {

            //if (string.IsNullOrEmpty(encryptedData))
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            return await SendInstructionAndNamesInternal(package, username);
        }
        private async Task<IActionResult> SendInstructionAndNamesInternal(InstructionPackage package, string? username)
        {

            string? personnelNumberOfSignedBy = await _dataService.UserNameToTableName(username);

            if (package == null)
            {
                return BadRequest("Empty or null encrypted payload is not acceptable.");
            }
            try
            {
                // Decrypt data here
                //string jsonData = await DecryptAsync(encryptedData);
                //InstructionPackage package = System.Text.Json.JsonSerializer.Deserialize<InstructionPackage>(jsonData);

                if (!ModelState.IsValid)
                {
                    // Return a 400 BadRequest with detailed information about what validation rules were violated
                    return BadRequest(ModelState);
                }
                if (string.IsNullOrEmpty(username))
                {
                    //return Unauthorized("Username claim of Chief not found.");
                    return Unauthorized("username claim of Chief(or User when used by Coordinator) не найден.");
                }
                string? connectionString = null;
                int departmentId = await GetDepartmentIdFromUserName(username);

                connectionString = GetConnectionStringByDepartmentId(departmentId);

                DBProcessor example = new DBProcessor(connectionString);
                // Here you would include any logic to process the package, e.g., storing it in a database asynchronously
                bool result = await example.ProcessDataAsync(package, personnelNumberOfSignedBy);
                if (result)
                {
                    return Ok($"Received and processed successfully: {package.InstructionCause} instructions with {package.NamesAndBirthDates.Count} names.");
                }
                else
                {
                    return BadRequest("Oops, something went wrong in ProcessDataAsync :(");
                }
            }
            catch (Exception ex)
            {
                // If an error occurs, log it and return a BadRequest with the error message
                Console.WriteLine(ex.ToString());
                return BadRequest($"An error occurred while processing the instruction and names: {ex.Message}");
            }
        }


        

        private async Task<string> DecryptAsync(string encryptedData)
        {
            // Placeholder for decryption logic
            // Replace this with your actual decryption method which might involve asynchronous operations
            await Task.Delay(10); // Simulating an async operation
                                  // For demonstration, just assume it returns the string directly
            return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedData));
        }

        [HttpGet("import-into-db")] // IMPORT DATA FROM EXCEL INTO DB
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> ImportIntoDBAsync()
        {
            try
            {
                string excelFilePath = GetNewestExcelFilePath();
                DBProcessor example = new DBProcessor();
                await example.ImportDataFromExcelAsync(example.GetConnectionString(), excelFilePath);
                return Ok();
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        private string GetNewestExcelFilePath()
        {
            var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");
            if (!Directory.Exists(directoryPath))
            {
                throw new FileNotFoundException("Directory not found.");
            }

            var directoryInfo = new DirectoryInfo(directoryPath);
            var newestFile = directoryInfo.EnumerateFiles()
                              .Where(f => f.Extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
                                       || f.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                              .OrderByDescending(f => f.LastWriteTime)
                              .FirstOrDefault();

            if (newestFile == null)
            {
                throw new FileNotFoundException("No Excel files found in the directory.");
            }

            return newestFile.FullName;
        }

        [HttpPost("add-new-instruction-into-db")]  //ПРОДОЛЖИ С ЭТОГО МЕСТА ИСКАТЬ КАК ВПИХНУТЬ ВВОДНЫЙ ИНСТРУКТАЖ В ФУНКЦИЮ ПО ДОБАВЛЕНИЮ ОБЫЧНОЙ ИНСТРУКЦИИ!
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> AddNewInstructionIntoDB([FromBody] FullCustomInstruction fullInstruction)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Username claim of Chief not found.");
                }

                // Call the internal method and pass the necessary parameters
                var result = await AddNewInstructionInternal(fullInstruction, username);

                // Return the appropriate HTTP response based on the result
                if (result.Success)
                {
                    return Ok(result.Instruction);
                }
                else
                {
                    return BadRequest(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return BadRequest("Can't add instruction to DB. Most probably cause of instruction already exists in DB");
            }
        }

        private async Task<(bool Success, Instruction Instruction, string ErrorMessage)> AddNewInstructionInternal(FullCustomInstruction fullInstruction, string username)
        {
            try
            {
                Instruction instruction = fullInstruction._instruction;
                List<string> paths = fullInstruction._paths;

                List<FilePath> pathsOfFilePath = paths.Select(path => new FilePath
                {
                    file_path = path // Placeholder instruction_id will be updated after saving instruction! So don't worry :) its not gonna be null;
                }).ToList();

                instruction.begin_date = DateTime.UtcNow;

                int departmentId = await GetDepartmentIdFromUserName(username);
                var dbContext = GetDbContextForDepartment(departmentId);
                if (dbContext == null) 
                {
                    return (false, null, "Not Implemented case in function AddNewInstructionInternal, check for error there");
                }
                await SaveInstructionWithFilePaths(dbContext, instruction, pathsOfFilePath);

                return (true, instruction, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (false, null, "Can't add instruction to DB. Most probably cause of instruction already exists in DB");
            }
        }

        private async Task SaveInstructionWithFilePaths(ApplicationDBContextBase context, Instruction instruction, List<FilePath> pathsOfFilePath)
        {
            context.Instructions.Add(instruction);
            await context.SaveChangesAsync();

            foreach (var filePath in pathsOfFilePath)
            {
                filePath.instruction_id = instruction.instruction_id;
                context.FilePaths.Add(filePath);
            }

            await context.SaveChangesAsync();
        }

        private async Task<int> GetDepartmentIdFromUserName(string username)
        {
            var user = await _userContext.Users
            .Where(u => u.username == username)
            .Select(u => new { u.department_id })
            .FirstOrDefaultAsync();
            if (user == null)
            {
                return -1;
            }
            return user.department_id;
        }
        [HttpGet("download-list-of-all-departments-and-employees")]
        [Authorize(Roles = "Management, Administrator")]
        public async Task<IActionResult> DownloadListOfDepartmentsAndEmployeesFromDB()
        {
            return await DownloadListOfDepartmentsAndEmployeesFromDBInternal();
        }

        private async Task<IActionResult> DownloadListOfDepartmentsAndEmployeesFromDBInternal()
        {
            try
            {
                var departments = await _userContext.Departments.ToListAsync();
                if (departments.Count == 0)
                {
                    //return something that shows that there are no departments.
                }
                List<Dept> deptList = new List<Dept>();
                foreach (var department in departments)
                {
                    using (var dbContext = GetDbContextForDepartment(department.department_id))
                    {
                        Dept new_dept = new Dept();
                        new_dept.Name = department.department_name;

                        // Fetch employees for the current department
                        new_dept.Employees = await dbContext.Department_employees
                                                            .ToListAsync();

                        deptList.Add(new_dept);


                    }
                }
                    string deptListSerialized = JsonConvert.SerializeObject(deptList);
                return Ok(deptListSerialized);
            }
            catch
            {
                return StatusCode(500, "Internal Server Error: Could not retrieve departments.");
            }
        }

        [HttpGet("download-list-of-departments")]
        [Authorize(Roles = "Coordinator, Management, Administrator")]
        public async Task<IActionResult> DownloadListOfDepartmentsFromDB()
        {
            try
            {
                var connectionString = _dataService._configuration.GetConnectionString("DefaultConnectionForUsers");
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextUsers>();
                optionsBuilder.UseSqlServer(connectionString);

                using (var context = new ApplicationDbContextUsers(optionsBuilder.Options))
                {
                    var departmentNames = await context.Departments
                                                  .Select(dept => dept.department_name)
                                                  .ToListAsync();

                    return Ok(departmentNames);
                }
            }
            catch (Exception ex)
            {
                // Log the error details here for debugging purposes
                return StatusCode(500, "Internal Server Error: Could not retrieve departments.");
            }
        }

        [HttpGet("get-roles-for-newcomer")]
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> DownloadListOfRolesFromDB()
        {
            try
            {
                var connectionString = _dataService._configuration.GetConnectionString("DefaultConnectionForUsers");
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextUsers>();
                optionsBuilder.UseSqlServer(connectionString);

                using (var context = new ApplicationDbContextUsers(optionsBuilder.Options))
                {
                    var roleTypes = await context.Roles
                                                  .Select(role => role.roletype)
                                                  .ToListAsync();

                    return Ok(roleTypes);
                }
            }
            catch (Exception ex)
            {
                // Log the error details here for debugging purposes
                return StatusCode(500, "Internal Server Error: Could not retrieve departments.");
            }
        }


        [HttpPost("insert-new-employee")] // TODO: Это надо переделать, так как сервер возвращает что всё хорошо даже когда это не так :/
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> InsertNewcomerIntoDb([FromBody] Employee newcomer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ApplicationDBContextBase context;

            switch (newcomer.department)
            {
                case "Общестроительный отдел":
                    context = _contextGeneralConstr;
                    break;
                case "Технический отдел":
                    context = _contextTechnicalDepartment;
                    break;
                case "Начальство":
                    context = _contextManagement;
                    break;
                default:
                    return BadRequest("Invalid department");
            }

            bool employeeExists = await context.Department_employees
                .AnyAsync(e => e.personnel_number == newcomer.personnel_number);
            bool tablePNExists = await DoesTableExistAsync(context, newcomer.personnel_number);

            if (employeeExists)
            {
                return BadRequest("An employee with the same PersonnelNumber already exists.");
            }
            if (tablePNExists)
            {
                return BadRequest("A table with the same PersonnelNumber already exists.");
            }

            try
            {
                string connectionString = context.Database.GetDbConnection().ConnectionString;
                await DBProcessor.CreateTableDIAsync(newcomer.personnel_number, connectionString);
            }
            catch (Exception ex)
            {
                return BadRequest($"A table with new personnel number can't be created: {ex.Message}");
            }

            context.Department_employees.Add(newcomer);
            int rowsAffected = await context.SaveChangesAsync();

            if (rowsAffected > 0)
            {
                return Ok("Employee inserted into DB");
            }
            return BadRequest("Something went wrong, employee not inserted. Check InsertNewcomerIntoDb.");
        }


        private async Task<bool> DoesTableExistAsync<TContext>(TContext context, string tableName) where TContext : DbContext
        {
            var sqlQuery = "SELECT CASE WHEN EXISTS (" +
                           "SELECT * FROM INFORMATION_SCHEMA.TABLES " +
                           "WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName) " +
                           "THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END";

            var parameter = new Microsoft.Data.SqlClient.SqlParameter("@tableName", tableName);

            var exists = await context.Database.ExecuteSqlRawAsync(sqlQuery, parameter);
            return exists == 1;
        }


        [HttpPost("get-login-and-password-for-newcommer")]
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> GenerateNewPasswordAndLogin([FromBody] List<string> someInfoAboutNewUser)
        {
            UserTemp newUser = new UserTemp(someInfoAboutNewUser[0], someInfoAboutNewUser[1], someInfoAboutNewUser[2], someInfoAboutNewUser[3], _dataService);

            bool isNeededToCreateInitialInstruction = someInfoAboutNewUser[4] == "True";

            try
            {
                if (newUser.UserRoleIndex is null)
                {
                    return BadRequest($"userRole {someInfoAboutNewUser[3]} is invalid to be put into Database, returning null");
                }

                var user = new User
                {
                    username = newUser.Login,
                    password_hash = newUser.HashedPassword,
                    user_role = newUser.UserRoleIndex.Value,
                    current_personnel_number = newUser.PersonnelNumber,
                    department_id = newUser.DepartmentId.Value,
                    desk_number = newUser.DeskNumber
                };

                _userContext.Users.Add(user);
                int result = await _userContext.SaveChangesAsync();

                if (result > 0)
                {
                    var whatever = new Tuple<string, string>(newUser.Login, newUser.Password);
                    string serialized = JsonConvert.SerializeObject(whatever);

                    if (isNeededToCreateInitialInstruction)
                    {
                        await FindNewEmployeeAndCreateInitialInstruction(newUser.PersonnelNumber, newUser.DepartmentId);
                    }

                    return Ok(serialized);
                }
                else
                {
                    Console.WriteLine("Error inserting user.");
                    return BadRequest($"Couldn't insert user: {newUser.Login} into database");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return BadRequest("Something went wrong while inserting user");
            }
        }


        private async Task FindNewEmployeeAndCreateInitialInstruction(string personnelNumber, int? departmentId)
        {
            try
            {
                var user = await _userContext.Users
                    .Where(u => u.current_personnel_number == personnelNumber && u.department_id == departmentId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    Console.WriteLine("User not found.");
                    return;
                }

                var initialInstruction = new Instruction
                {
                    cause_of_instruction = $"Вводный инструктаж для {personnelNumber}",
                    begin_date = DateTime.UtcNow,
                    end_date = DateTime.UtcNow.AddMonths(1),
                    path_to_instruction = null,
                    is_assigned_to_people = true, //TODO, это должно выполняться после того как инструктаж направлен человеку! А не здесь.
                    type_of_instruction = 0, // 0 represents Вводный инструктаж
                };

                var fullCustomInstruction = new FullCustomInstruction
                {
                    _instruction = initialInstruction,
                    _paths = new List<string?> { null }
                };

                var result = await AddNewInstructionInternal(fullCustomInstruction, user.username);

                if (!result.Success)
                {
                    Console.WriteLine(result.ErrorMessage);
                }
                else
                {
                    var result2 = await AssignNewInstructionToUser(fullCustomInstruction, departmentId, personnelNumber);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task<bool> AssignNewInstructionToUser(FullCustomInstruction fullCustomInstruction, int? departmentId, string personnelNumber)
        {

            string? userName = User.FindFirst(ClaimTypes.Name)?.Value;
            string? personnelNumberOfSignedBy = await _dataService.UserNameToTableName(userName);

            if (departmentId == null) return false;
            int departmentIdNotNull = departmentId.Value;

            ApplicationDBContextBase dbContext = GetDbContextForDepartment(departmentIdNotNull);
            if (dbContext == null) return false;

            try
            {
                string connectionString = dbContext.Database.GetDbConnection().ConnectionString;
                using (var connection = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Get the instruction ID from the fullCustomInstruction object
                            var instructionId = fullCustomInstruction._instruction.instruction_id;

                            // Prepare the list of personnel numbers to notify
                            List<string> personnelNumbers = new List<string> { personnelNumber };

                            DBProcessor dBProcessor = new DBProcessor(connectionString);
                            // Call the SendNotificationToPeopleAsync method to send the notification
                            var isNotificationSent = await dBProcessor.SendNotificationToPeopleAsync(personnelNumbers, instructionId, connection, transaction, personnelNumberOfSignedBy);

                            if (!isNotificationSent)
                            {
                                transaction.Rollback();
                                return false;
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                            transaction.Rollback();
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        [HttpGet("get-list-of-people-init-instructions")]
        [Authorize(Roles = "Coordinator, Administrator")]
        public async Task<IActionResult> GetNamesForInitialInstructions()
        {
            return await GetNamesForInitialInstructionsInternal();
        }

        private async Task<IActionResult> GetNamesForInitialInstructionsInternal()
        {
            List<ApplicationDBContextBase> dbContexts = DownloadListOfDBContextFromDB();
            List<InstructionDto> results = new List<InstructionDto>();
            List<string> errors = new List<string>();

            foreach (ApplicationDBContextBase dbContext in dbContexts)
            {
                var instructions = await dbContext.Instructions
                                                  .Where(i => EF.Functions.Like(i.cause_of_instruction, "Вводный инструктаж для %"))
                                                  .ToListAsync();

                foreach (var instruction in instructions)
                {
                    string? personnelNumber = instruction.cause_of_instruction.ExtractTenDigitNumber();
                    if (string.IsNullOrEmpty(personnelNumber))
                    {
                        errors.Add($"Не удалось извлечь 10-значный номер из: {instruction.cause_of_instruction}");
                        continue;
                    }

                    var employee = await dbContext.Department_employees
                        .Where(e => e.personnel_number == personnelNumber)
                        .Select(e => new Employee
                        {
                            full_name = e.full_name,
                            birth_date = e.birth_date
                        })
                        .FirstOrDefaultAsync();
                    if (employee == null)
                    {
                        errors.Add($"Не найден сотрудник с персональным номером: {personnelNumber}");
                        continue;
                    }

                    results.Add(new InstructionDto
                    {
                        InstructionId = instruction.instruction_id,
                        TenDigitNumber = personnelNumber,
                        Name = employee.full_name,
                        BirthDate = employee.birth_date
                    });
                }
            }

            if (errors.Any())
            {
                return BadRequest(new { Errors = errors });
            }

            return Ok(results);
        }

        private List<ApplicationDBContextBase> DownloadListOfDBContextFromDB()
        {
            List<ApplicationDBContextBase> dbContexts = new List<ApplicationDBContextBase>();
            dbContexts.Add(_contextGeneralConstr);
            dbContexts.Add(_contextTechnicalDepartment);
            // Add some other stuff if needed :)

            return dbContexts;
        }

        [HttpGet("get-not-passed-instructions-for-chief")]
        [Authorize(Roles = "ChiefOfDepartment, Administrator")]
        public async Task<IActionResult> GetNotPassedInstructionForChief()
        {

            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized("username для данного начальника не найден");
            }

            int departmentId = await GetDepartmentIdFromUserName(username);

            var dbContext = GetDbContextForDepartment(departmentId);
            if (dbContext == null)
            {
                return NotFound("для данного начальника не найден отдел! (GetNotPassedInstructionForChief)");
            }

            return await CheckPassingTheInstructionsBeforeReturningTheData(dbContext);
        }

        

        private static async Task<int> ExecuteScalarAsyncInt(ApplicationDBContextBase dbContext, string sql, params object[] parameters)
        {
            using (var command = dbContext.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                if (command.Connection.State == System.Data.ConnectionState.Closed)
                {
                    await command.Connection.OpenAsync();
                }

                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        private static async Task<string> ExecuteScalarAsyncString(ApplicationDBContextBase dbContext, string sql, params object[] parameters)
        {
            using (var command = dbContext.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                if (command.Connection.State == System.Data.ConnectionState.Closed)
                {
                    await command.Connection.OpenAsync();
                }

                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }

                var result = await command.ExecuteScalarAsync();
                return result.ToString();
            }
        }


        private async Task<IActionResult> CheckPassingTheInstructionsBeforeReturningTheData(ApplicationDBContextBase dbContext)
        {
            var instructionsToCheck = await dbContext.Instructions
                .Where(i => !i.is_passed_by_everyone)
                .ToListAsync();
            List<InstructionForChief> instructionsForChiefList = new List<InstructionForChief>();

            if (!instructionsToCheck.Any())
            {
                Console.WriteLine("No instructions to check.");
                var instructionsForChiefList_Serialized = JsonConvert.SerializeObject(instructionsForChiefList);
                return Ok(instructionsForChiefList_Serialized); // Will return empty List
            }

            try
            {
                var tenDigitTables = dbContext.GetTenDigitTableNames();

                foreach (var instructionToCheck in instructionsToCheck)
                {
                    int instructionId = instructionToCheck.instruction_id;

                    List<(string personnelNumber, string personName)> instructionIsNotPassedByListOfPeople = new List<(string, string)>();
                    List<(string personnelNumber, string personName)> instructionIsPassedByListOfPeople = new List<(string, string)>();

                    foreach (string? tableName in tenDigitTables)
                    {
                        if (string.IsNullOrEmpty(tableName))
                        {
                            Console.WriteLine("Table name is null or empty.");
                            continue;
                        }

                        var sqlQuery = $"SELECT COUNT(1) FROM [{tableName}] WHERE [instruction_id] = @instructionId AND is_instruction_passed = 0";
                        var result = await ExecuteScalarAsyncInt(dbContext, sqlQuery, new Microsoft.Data.SqlClient.SqlParameter("@instructionId", instructionId));

                        var sqlQueryToRecieveFullName = $"SELECT full_name FROM [{DBProcessor.tableName_sql_MainName.Split(".")[1]}] WHERE [personnel_number] = @PN";
                        var resultFullName = await ExecuteScalarAsyncString(dbContext, sqlQueryToRecieveFullName, new Microsoft.Data.SqlClient.SqlParameter("@PN", tableName));

                        if (result > 0)
                        {
                            instructionIsNotPassedByListOfPeople.Add((tableName, resultFullName));
                        }
                        else
                        {
                            var sqlQuery2 = $"SELECT COUNT(1) FROM [{tableName}] WHERE [instruction_id] = @instructionId AND is_instruction_passed = 1";
                            var result2 = await ExecuteScalarAsyncInt(dbContext, sqlQuery2, new Microsoft.Data.SqlClient.SqlParameter("@instructionId", instructionId));

                            var sqlQueryToRecieveFullName2 = $"SELECT full_name FROM [{DBProcessor.tableName_sql_MainName.Split(".")[1]}] WHERE [personnel_number] = @PN";
                            var resultFullName2 = await ExecuteScalarAsyncString(dbContext, sqlQueryToRecieveFullName2, new Microsoft.Data.SqlClient.SqlParameter("@PN", tableName));

                            if (result2 > 0)
                            {
                                instructionIsPassedByListOfPeople.Add((tableName, resultFullName2));
                            }
                        }
                    }

                    if (!instructionIsNotPassedByListOfPeople.Any() && instructionIsPassedByListOfPeople.Any())
                    {
                        var instructionToUpdate = await dbContext.Instructions.FirstOrDefaultAsync(i => i.instruction_id == instructionId);

                        if (instructionToUpdate != null)
                        {
                            instructionToUpdate.is_passed_by_everyone = true;
                            dbContext.Instructions.Update(instructionToUpdate);
                            await dbContext.SaveChangesAsync();
                            continue;
                        }
                    }

                    var persons = instructionIsNotPassedByListOfPeople.Select(p => new InstructionForChief.PersonStatus
                    {
                        PersonnelNumber = p.personnelNumber,
                        PersonName = p.personName,
                        Passed = false
                    }).Concat(instructionIsPassedByListOfPeople.Select(p => new InstructionForChief.PersonStatus
                    {
                        PersonnelNumber = p.personnelNumber,
                        PersonName = p.personName,
                        Passed = true
                    })).ToList();

                    InstructionForChief instructionForChief = new InstructionForChief()
                    {
                        InstructionId = instructionId,
                        BeginDate = instructionToCheck.begin_date,
                        EndDate = instructionToCheck.end_date,
                        CauseOfInstruction = instructionToCheck.cause_of_instruction,
                        TypeOfInstruction = instructionToCheck.cause_of_instruction,
                        Persons = persons,
                    };

                    instructionsForChiefList.Add(instructionForChief);
                }

                var instructionsForChiefList_Serialized = JsonConvert.SerializeObject(instructionsForChiefList);
                return Ok(instructionsForChiefList_Serialized);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                Console.WriteLine(ex);
                return BadRequest("Упс, что-то пошло не так в CheckPassingTheInstructionsByChief, проверь!");
            }
        }


        public class UserTemp
        {
            private readonly MyDataService _dataService;
            public string PersonnelNumber { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
            public string HashedPassword { get; set; }
            public int? DepartmentId { get; set; }
            public string? DeskNumber { get; set; }
            public int? UserRoleIndex { get; set; }

            public UserTemp(string personnelNumber, string departmentName, string deskNumber, string userRole, MyDataService dataService)
            {
                PersonnelNumber = personnelNumber;
                DepartmentId = departmentNameToId(departmentName);
                DeskNumber = deskNumber;
                _dataService = dataService;

                Random random = new Random();
                int randomNumber = random.Next(1000000, 9999999); // Generates a 7-digit number for User TODO: IF RANDOM NUMBER GENERATES THE SAME - CHECK THAT AND REGENERATE AGAIN UNTIL IT WILL GENERATE NEW ONE!
                Login = $"User{randomNumber}";
                Password = Login;
                HashedPassword = Encryption_Kotova.HashPassword(Password);
                UserRoleIndex = IndexFromUserRole(userRole);
                
            }

            private int? IndexFromUserRole(string userRole)
            {
                var temporaryConnectionString = _dataService._configuration.GetConnectionString("DefaultConnectionForUsers");
                var temporaryOptionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextUsers>();
                temporaryOptionsBuilder.UseSqlServer(temporaryConnectionString);

                using (var context = new ApplicationDbContextUsers(temporaryOptionsBuilder.Options))
                { 

                    var roleId = context.Roles
                                          .Where(r => r.roletype == userRole)
                                          .Select(r => r.roleid)
                                          .FirstOrDefault();

                    List<string> validRoles = new List<string> { "user", "chief of department", "management"};
                    if (validRoles.Contains(userRole))
                    {
                        return roleId;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            private int departmentNameToId(string departmentName) // Можешь переделать чтобы брались данные из таблицы с id и именами отдела
            {
                switch (departmentName)
                {
                    case "Общестроительный отдел":
                        return 1;
                    case "Технический отдел":
                        return 2;
                    case "Начальство":
                        return 5;
                    default:
                        return -1;
                }
            }

            public override string ToString()
            {
                return $"Personnel Number: {PersonnelNumber}, Login: {Login}, Password: {Password}";
            }
        }
    }


    public class AuthenticationController : ControllerBase
    {
        private readonly LegacyAuthenticationService _legacyAuthService;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContextUsers _context;
        private readonly ChiefsManager _chiefsManager;
        private readonly JwtTokenValidator _jwtTokenValidator;
        public AuthenticationController(LegacyAuthenticationService legacyAuthService, IConfiguration configuration, ApplicationDbContextUsers context, ChiefsManager chiefsManager, JwtTokenValidator jwtTokenValidator)
        {
            _legacyAuthService = legacyAuthService;
            _configuration = configuration;
            _context = context;
            _chiefsManager = chiefsManager;
            _jwtTokenValidator = jwtTokenValidator;
        }



        *//*[HttpPost("validate-token")]
        public IActionResult ValidateToken([FromHeader(Name = "Authorization")] string authorization)
        {
            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
            {
                return Unauthorized();
            }

            var token = authorization.Substring("Bearer ".Length).Trim();
            var principal = _jwtTokenValidator.ValidateToken(token);

            if (principal == null)
            {
                return Unauthorized();
            }

            // Optionally: You could regenerate a new token here if needed
            // var newToken = GenerateNewToken(principal);
            // return Ok(new { token = newToken });

            return Ok("Token is valid.");
        }*//*

        [HttpPost("validate-token")]
        public IActionResult ValidateToken([FromBody] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Token is null or empty.");
                return Unauthorized("Validation failed, Token is null or empty.");
            }

            var principal = _jwtTokenValidator.ValidateToken(token);

            if (principal == null)
            {
                Console.WriteLine("Token validation failed.");
                return Unauthorized("Validation failed, token is invalid");
            }

            Console.WriteLine("Token validation succeeded.");
            return Ok();
        }



        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserForAuthentication model)
        {
            User userTemp = await GetUserByUsername(model.username);
            if (userTemp == null)
            {
                return BadRequest($"user under name {model.username} wasn't found");
            }
            if (model.time_for_being_authenticated <= 0)
            {
                return BadRequest("time for being authenticated was not correct, type valid time");
            }
            
            (bool?,User?) authenticationModel = _legacyAuthService.PerformLogin(userTemp, model.password);
            if (authenticationModel.Item1 == true)
            {

                if (await _chiefsManager.IsChiefOnlineAsync(authenticationModel.Item2.department_id))
                {

                    return CustomForbid("Current department already have Chief Authenticated. Ask him to close application and then after 1 minute - open your application.");
                }


                try
                {
                    User? user = authenticationModel.Item2;
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, model.username),
                        new Claim(ClaimTypes.Role, RoleModelIntToString(user.user_role)),
                    };

                    string secret = _configuration["JwtConfig:Secret"]; // Remember to store this securely and not hardcode in production
                    var token = GenerateJwtToken(claims, secret, model.time_for_being_authenticated);

                    return Ok(new { Token = token, Message = "User authenticated successfully." });
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(ex.Message);
                }
                
            }
            else if (authenticationModel.Item1 == null)
            {
                return Unauthorized("User doesn't have personnel number yet, wait when you gonna have personnel number");
            }
            else
            {
                return Unauthorized("Authentication failed.");
            }
        }

        public IActionResult CustomForbid(string message)
        {
            var result = new ObjectResult(new { Message = message })
            {
                StatusCode = (int)HttpStatusCode.Forbidden
            };
            return result;
        }

        private async Task<User> GetUserByUsername(string username)
        {
            User user = null;

            string query = "SELECT * FROM Users WHERE Username = @Username";

            string _connectionString = _configuration.GetConnectionString("DefaultConnectionForUsers");

            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", username);

                    using (Microsoft.Data.SqlClient.SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            user = new User
                            {
                                username = reader["username"].ToString(),
                                password_hash = reader["password_hash"].ToString(),
                                user_role = reader.GetInt32(reader.GetOrdinal("user_role")),
                                current_personnel_number = reader["current_personnel_number"].ToString(),
                                current_email = reader["current_email"].ToString(),
                                department_id = reader.GetInt32(reader.GetOrdinal("department_id")),
                                desk_number = reader["desk_number"].ToString(),
                                // Initialize other properties as needed
                            };
                        }
                    }
                }
            }

            return user;
        }


        [HttpPatch]
        [Route("change_credentials")]
        [Authorize]
        public async Task<IActionResult> ChangeCredentials([FromBody] UserCredentials credentials)
        {
            // Retrieve the JWT token from the Authorization header
            var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            string? jwtToken = authorizationHeader?.StartsWith("Bearer ") == true ? authorizationHeader.Substring("Bearer ".Length).Trim() : null;
            if (jwtToken == null || jwtToken.Length == 0)
            {
                return BadRequest("JWT token is null or empty");
            }
            string? user = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrWhiteSpace(user))
            {
                return BadRequest("user is null or empty");
            }
            CredentialValidation credentialValidation = new CredentialValidation();
            if (credentialValidation.CheckForValidation(credentials, user))
            {
                try
                {
                    return await UpdateCredentialsForUserInDB(credentials, user);
                }
                catch
                {
                    return BadRequest("Couldn't update the user credentials in DB");
                }
            }
            else
            {
                return BadRequest("checkForValidation returned false");
            }

            
        }


        private async Task<IActionResult> UpdateCredentialsForUserInDB(UserCredentials credentials, string user)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnectionForUsers");
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextUsers>();
            optionsBuilder.UseSqlServer(connectionString);

            using (var context = new ApplicationDbContextUsers(optionsBuilder.Options))
            {
                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var newUserExistInDB = await context.Users.FirstOrDefaultAsync(u => u.username == credentials.Login);
                        if (newUserExistInDB != null)
                        {
                            return BadRequest("username is already taken/exist in DB");
                        }

                        // Fetch the user from the database
                        var userToUpdate = await context.Users.FirstOrDefaultAsync(u => u.username == user);

                        if (userToUpdate != null)
                        {
                            // Update user details
                            userToUpdate.username = credentials.Login;
                            userToUpdate.password_hash = Encryption_Kotova.HashPassword(credentials.Password);
                            userToUpdate.current_email = credentials.Email;

                            await context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            return Ok();
                        }
                        else
                        {
                            throw new Exception("User not found");
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        Console.WriteLine($"An error occurred: {ex.Message}");
                        return StatusCode(500, "Internal server error. Please try again later.");
                    }
                }
            }
        }


        [Authorize]
        [HttpGet("securedata")]
        public IActionResult GetSecureData()
        {
            return Ok("This is secured data.");
        }

        private string RoleModelIntToString(int user_role)
        {
            if (user_role == 1)
            {
                return "User";
            }
            else if (user_role == 2)
            {
                return "ChiefOfDepartment";
            }
            else if (user_role == 3)
            {
                return "Coordinator";
            }
            else if (user_role == 4)
            {
                return "Management";
            }
            else if (user_role == 5)
            {
                return "Administrator";
            }
            throw new ArgumentException($"user_role:{user_role} is not valid, something is wrong!");
        }

        private bool CheckForValidPersonnelNumber(string input)
        {
            string pattern = @"^\d{10}$";

            if (Regex.IsMatch(input, pattern))
            {
                return true;
            }
            return false;
        }
        public string GenerateJwtToken(List<Claim> claims, string secret, int timeForExpiration)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "yourdomain.com",
                audience: "yourdomain.com",
                claims: claims,
                expires: DateTime.Now.AddMinutes(timeForExpiration),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

}*/