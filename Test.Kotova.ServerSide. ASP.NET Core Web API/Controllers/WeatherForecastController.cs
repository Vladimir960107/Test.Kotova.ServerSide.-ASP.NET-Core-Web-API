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
            if (file == null || file.Length == 0)
                return BadRequest("Please upload a file.");

            // Check the MIME type to see if it's an Excel file
            string[] permittedMimeTypes = new string[]
            {
                "application/vnd.ms-excel",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            bool isPermittedMimeType = permittedMimeTypes.Contains(file.ContentType);
            bool isPermittedExtension = extension == ".xls" || extension == ".xlsx";

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
                var path = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles", file.FileName);

                // Ensure the directory exists
                var directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    if (directory is null)
                    {
                        throw new Exception("directory for UploadedFiles is empty");
                    }
                    Directory.CreateDirectory(directory);
                }

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Ok(new { file.FileName, file.Length });
            }
            catch (Exception ex)
            {
                // Log the exception here

                // Return the exception details
                return StatusCode(500, $"Internal server error: {ex}");
            }
        }
    }
}
