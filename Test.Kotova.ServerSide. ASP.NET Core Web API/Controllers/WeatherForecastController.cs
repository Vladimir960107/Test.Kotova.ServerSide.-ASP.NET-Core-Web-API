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

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InstructionsController : ControllerBase
    {
        private readonly MyDataService _dataService;

        public InstructionsController(MyDataService dataService)
        {

            _dataService = dataService;
        }

        [Authorize]
        [HttpGet("greeting")]
        public IActionResult GetGreeting()
        {
            return Ok("Hello, World!");
        }

        [Authorize]
        [HttpGet("get_instructions_for_user")]
        public async Task<IActionResult> GetNotifications()
        {
            string? userName = User.FindFirst(ClaimTypes.Name)?.Value;
            string? userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            string? tableNameForUser = await _dataService.UserNameToTableName(userName);
            if (tableNameForUser == null) { return BadRequest($"The personelNumber for this user isn't found. Wait till you have personel number"); }
            List<Dictionary<string, object>> whatever = await _dataService.ReadDataFromDynamicTable(tableNameForUser);
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
            if (tableNameForUser == null) { return BadRequest($"The personelNumber for this user isn't found. Wait till you have personel number"); }
            
            if (await passInstructionIntoDb(dictionaryOfInstruction, tableNameForUser))
            {
                return Ok("Instruction Is passed, information added to Database");
            }
            else
            {
                return NotFound("Instruction was not found in DB!");
            }
        }

        private async Task<bool> passInstructionIntoDb(Dictionary<string, object> jsonDictionary, string tableNameForUser)
        {
            var connectionString = _dataService._configuration.GetConnectionString("DefaultConnectionForNotifications");
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDBNotificationContext>();
            optionsBuilder.UseSqlServer(connectionString);

            string tableName = DBProcessor.tableName_Instructions_sql;
            string columnName = DBProcessor.tableName_sql_INSTRUCTIONS_cause;

            string instructionIdColumn = DBProcessor.tableName_sql_USER_instruction_id; // The column name for instruction ID in the user table
            string isPassedColumn = DBProcessor.tableName_sql_USER_is_instruction_passed; // The column name for isPassed in the user table

            //string sqlQuery = @$"SELECT * FROM [{tableName.Split('.')[1]}] WHERE [{columnName}] = @value";
            string sqlQuery = @$"SELECT * FROM [{tableName.Split('.')[1]}] WHERE [{columnName}] = @value"; // here {tableName.Split('.')[1]} == {Instructions}
            string sqlQueryChangePassedVariable = @$"UPDATE [{tableNameForUser}]
                    SET [{isPassedColumn}] = 1
                    WHERE [{instructionIdColumn}] = @instructionId";

            using (var context = new ApplicationDBNotificationContext(optionsBuilder.Options))
            {
                var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = sqlQuery;
                    DbParameter param = command.CreateParameter();
                    param.ParameterName = "@value";
                    param.Value = ConvertJsonElement(jsonDictionary[columnName]);
                    command.Parameters.Add(param);
                    int rowCount = 0;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        
                        while (await reader.ReadAsync())
                        {
                            rowCount++;
                        }

                        
                    }
                    if (rowCount == 1)
                    {
                        // Close the reader before executing the update query
                        command.Parameters.Clear(); // Clear previous parameters

                        // Setting up and executing the update query
                        command.CommandText = sqlQueryChangePassedVariable;
                        DbParameter instructionIdParam = command.CreateParameter();
                        instructionIdParam.ParameterName = "@instructionId";
                        instructionIdParam.Value = ConvertJsonElement(jsonDictionary[DBProcessor.tableName_sql_USER_instruction_id]); // Replace "instructionIdKey" with the actual key from jsonDictionary that corresponds to the instruction ID
                        command.Parameters.Add(instructionIdParam);

                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        return rowsAffected > 0;
                    }
                    else if (rowCount > 1)
                    {
                        throw new Exception("Instructions with the same cause name were found in multiple quantities!");
                    }
                    else
                    {
                        Console.WriteLine("No rows matched the criteria.");
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
        public async Task<IActionResult> UploadExcelFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Please upload a file.");

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
                /*
                if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(directoryPath))) // Added: Path traversal check
                {
                    return BadRequest("Invalid file path entered. (Path traversal attack)");
                }
                */
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
        public IActionResult SyncNamesWithDB()
        {
            try
            {
                DBProcessor example = new DBProcessor(); // Rename class into something more accurate, if you can.
                List<Tuple<string, string>> names_and_BirthDate = example.GetNames(example.GetConnectionString()); //Может заменить GetconnectionString на переменную или переместить функцию в этот файл?
                return Ok(Encryption_Kotova.EncryptListOfTuples(names_and_BirthDate));
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        [HttpGet("sync-instructions-with-db")] //Make this load every 1 minute or something.!!!!!
        public IActionResult SyncInstructionsWithDB()
        {
            try
            {
                DBProcessor example = new DBProcessor();
                List<Instruction> notifications = example.GetInstructions(example.GetConnectionString());
                string serialized = JsonConvert.SerializeObject(notifications);
                string encryptedData = Encryption_Kotova.EncryptString(serialized);
                return Ok(encryptedData);

            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }
        [HttpPost("send-instruction-and-names")]
        public async Task<IActionResult> ReceiveInstructionAndNames([FromBody] InstructionPackage package) //REWRITE IT IN CASE OF USING ENCRYPTED STUFF(JSON - STRING!)
        //public async Task<IActionResult> ReceiveInstructionAndNamesAsync([FromBody] string encryptedData)
        {

            //if (string.IsNullOrEmpty(encryptedData))
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
                DBProcessor example = new DBProcessor();
                // Here you would include any logic to process the package, e.g., storing it in a database asynchronously
                bool result = await example.ProcessDataAsync(package);
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

        [HttpGet("import-into-db")]
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
    }
    public class AuthenticationController : ControllerBase
    {
        private readonly LegacyAuthenticationService _legacyAuthService;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        public AuthenticationController(LegacyAuthenticationService legacyAuthService, IConfiguration configuration, ApplicationDbContext context)
        {
            _legacyAuthService = legacyAuthService;
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserForAuthentication model)
        {
            (bool?,User?) authenticationModel = await _legacyAuthService.PerformLogin(model.username, model.password);
            if (authenticationModel.Item1 == true)
            {
                try
                {
                    User? user = authenticationModel.Item2;
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, model.username),
                        new Claim(ClaimTypes.Role, RoleModelIntToString(user.user_role)),
                    };

                    string secret = _configuration["JwtConfig:Secret"]; // Remember to store this securely and not hardcode in production
                    var token = GenerateJwtToken(claims, secret);

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
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDBNotificationContext>();
            optionsBuilder.UseSqlServer(connectionString);

            using (var context = new ApplicationDBNotificationContext(optionsBuilder.Options))
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
                    userToUpdate.username = credentials.Login; // Assuming you want to change the username to the new login
                    userToUpdate.password_hash = credentials.Password; // This should be a hashed password
                    userToUpdate.current_email = credentials.Email;

                    // Save changes to the database
                    await context.SaveChangesAsync();
                    return Ok();
                }
                else
                {
                    // Handle the case where the user is not found
                    throw new Exception("User not found");
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
                return "Chief Of Department";
            }
            else if (user_role == 3)
            {
                return "Coordinator";
            }
            else if (user_role == 4)
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
        public string GenerateJwtToken(List<Claim> claims, string secret)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "yourdomain.com",
                audience: "yourdomain.com",
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

}