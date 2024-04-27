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

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet("greeting")]
        public IActionResult GetGreeting()
        {
            return Ok("Hello, World!");
        }

        [HttpPost("upload")]
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

                int index = DetermineNextFileIndex(directoryPath);

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
        private int DetermineNextFileIndex(string directoryPath) //Move this function to separate file and import here maybe?
        {
            var fileNames = Directory.GetFiles(directoryPath)
                                     .Select(Path.GetFileNameWithoutExtension)
                                     .Where(name => name.StartsWith("StandardName_"))
                                     .Select(name =>
                                     {
                                         int idx = name.LastIndexOf('_') + 1;
                                         return int.TryParse(name[idx..], out int index) ? index : (int?)null;
                                     })
                                     .Where(index => index.HasValue)
                                     .Select(index => index.Value)
                                     .OrderBy(index => index);

            int nextIndex = 1;
            if (fileNames.Any())
            {
                nextIndex = fileNames.Last() + 1;
            }

            return nextIndex;
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
                List<Tuple<string, string>> names_and_BirthDate = example.GetNames(example.GetConnectionString()); //����� �������� GetconnectionString �� ���������� ��� ����������� ������� � ���� ����?
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
                List<Notification> notifications = example.GetInstructions(example.GetConnectionString());
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

        public AuthenticationController(LegacyAuthenticationService legacyAuthService)
        {
            _legacyAuthService = legacyAuthService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] User model)
        {
            var user = await _legacyAuthService.PerformLogin(model.username, model.username);
            /*
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
            }
            */
            if (user is not false)
            {
                // Handle successful authentication, e.g., issue a token or set a cookie
                return Ok("User authenticated successfully.");
            }
            else
            {
                // Handle failed authentication
                return Unauthorized("Authentication failed.");
            }
        }
    }
}