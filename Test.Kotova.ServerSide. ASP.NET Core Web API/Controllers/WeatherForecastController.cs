using DocumentFormat.OpenXml.Drawing;
using Microsoft.AspNetCore.Mvc;
using Path = System.IO.Path;


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

            //CHECK FOR Path Traversal Attacks!



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

                // Ensure the directory exists
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
                string extension = Path.GetExtension(file.FileName);
                string newFileName = $"StandardName_{index}_{file.FileName}";
                string fullPath = Path.Combine(directoryPath, newFileName);

                    // Save the file
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Ok(new { FileName = file.FileName });
            }
            catch (Exception ex)
            {
                // Log the exception here

                // Return the exception details
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
                string filePath = GetNewestExcelFilePath(); // Use the new method
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
                ImportFromExcelIntoDB example = new ImportFromExcelIntoDB(); // Rename class into DBInformation or something.
                //List<string>names = GetNames(example.GetConnectionString()); // Where to add functions, from example or здесь создать эту функцию?
                //example.ImportDataFromExcel(example.GetConnectionString(), excelFilePath);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while processing your request: {ex.Message}");
            }
        }

        [HttpGet("import-into-db")]
        public IActionResult ImportIntoDB()
        {
            try
            {
                string excelFilePath = GetNewestExcelFilePath(); // Use the new method
                ImportFromExcelIntoDB example = new ImportFromExcelIntoDB();
                example.ImportDataFromExcel(example.GetConnectionString(), excelFilePath); 
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
}
