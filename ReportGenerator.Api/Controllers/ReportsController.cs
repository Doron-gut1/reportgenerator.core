using Microsoft.AspNetCore.Mvc;
using ReportGenerator.Api.Models;
using ReportGenerator.Api.Utilities;
using ReportGenerator.Core.Data;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Management;
using ReportGenerator.Core.Management.Enums;
using ReportGenerator.Core.Configuration;
using Microsoft.Extensions.Options;
using ReportGenerator.Api.Services;

namespace ReportGenerator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IParameterConverter _parameterConverter;
        private readonly IFileSystemService _fileSystemService;
        private readonly ILogger<ReportsController> _logger;
        private readonly ReportManager _reportManager;
        private readonly DataAccess _dataAccess;

        public ReportsController(
            IParameterConverter parameterConverter,
            IFileSystemService fileSystemService,
            ILogger<ReportsController> logger,
            IConfiguration configuration)
        {
            _parameterConverter = parameterConverter;
            _fileSystemService = fileSystemService;
            _logger = logger;
            
            // קבלת הגדרות מהקונפיגורציה
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var templatesFolder = configuration["ReportSettings:TemplatesFolder"];
            var outputFolder = configuration["ReportSettings:OutputFolder"];
            
            // SimpleLogger כבר מאותחל ב-Program.cs
            
            // יצירת ReportManager ישירות
            _reportManager = new ReportManager(
                connectionString,
                templatesFolder,
                outputFolder);
                
            // יצירת DataAccess ישירות  
            _dataAccess = new DataAccess(connectionString);
        }

        /// <summary>
        /// מפיק דוח בהתאם לפרמטרים שהתקבלו ומחזיר את הקובץ
        /// </summary>
        /// <param name="request">בקשה להפקת דוח</param>
        /// <returns>קובץ PDF או Excel</returns>
        [HttpPost("generate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateReport([FromBody] GenerateReportRequest request)
        {
            _logger.LogInformation("התקבלה בקשה להפקת דוח {ReportName}, מחלקה: {DepartmentId}", 
                request.ReportName, request.DepartmentId ?? "ברירת מחדל");

            try
            {
                // בדיקת תקינות המחלקה (אם התקבלה)
                if (!string.IsNullOrEmpty(request.DepartmentId) && !_fileSystemService.IsValidDepartment(request.DepartmentId))
                {
                    return BadRequest(new { error = $"מחלקה {request.DepartmentId} אינה תקפה" });
                }

                // המרת פרמטרים
                var outputFormat = _parameterConverter.ConvertOutputFormat(request.OutputFormat);
                var parameters = _parameterConverter.ConvertRequestParameters(request.Parameters);

                // הפקת הדוח עם שמירה מפורשת (כבר כולל שמירה)
                var result = await _reportManager.GenerateReportAndSave(
                    request.ReportName,
                    outputFormat,
                    null, // שימוש בנתיב ברירת מחדל
                    parameters);

                // הערה: השמירה כבר בוצעה ב-GenerateReportAndSave
                // אין צורך בשמירה נוספת כדי למנוע כפילות

                // קביעת סוג התוכן לפי הפורמט
                string contentType = request.OutputFormat.ToLower() == "pdf" ?
                    "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                // קביעת שם הקובץ
                string fileExtension = request.OutputFormat.ToLower() == "pdf" ? "pdf" : "xlsx";
                string fileName = $"{request.ReportName}_{DateTime.Now:yyyyMMdd_HHmmss}.{fileExtension}";

                // החזרת הקובץ
                return File(result, contentType, fileName);
            }
            catch (Exception ex)
            {
                // רישום השגיאה
                _logger.LogError(ex, "שגיאה בהפקת דוח {ReportName}", request.ReportName);

                // בדיקה אם זו שגיאה מוכרת
                if (ex is ReportNotFoundException)
                {
                    return NotFound(new { error = $"הדוח {request.ReportName} לא נמצא במערכת" });
                }

                // החזרת שגיאה כללית
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "שגיאה בהפקת הדוח", details = ex.Message });
            }
        }
        
        /// <summary>
        /// מפיק דוח ומחזיר raw bytes (עבור EXE clients)
        /// </summary>
        /// <param name="request">בקשה להפקת דוח</param>
        /// <returns>Raw bytes של קובץ PDF או Excel</returns>
        [HttpPost("generate-bytes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateReportBytes([FromBody] GenerateReportRequest request)
        {
            _logger.LogInformation("התקבלה בקשה להפקת דוח (bytes) {ReportName}, מחלקה: {DepartmentId}", 
                request.ReportName, request.DepartmentId ?? "ברירת מחדל");

            try
            {
                // בדיקת תקינות המחלקה (אם התקבלה)
                if (!string.IsNullOrEmpty(request.DepartmentId) && !_fileSystemService.IsValidDepartment(request.DepartmentId))
                {
                    return BadRequest(new { error = $"מחלקה {request.DepartmentId} אינה תקפה" });
                }

                // המרת פרמטרים
                var outputFormat = _parameterConverter.ConvertOutputFormat(request.OutputFormat);
                var parameters = _parameterConverter.ConvertRequestParameters(request.Parameters);

                // הפקת הדוח ללא שמירה
                var result = await _reportManager.GenerateReportBytesOnly(
                    request.ReportName,
                    outputFormat,
                    parameters);

                // קביעת סוג התוכן לפי הפורמט
                string contentType = request.OutputFormat.ToLower() == "pdf" ?
                    "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                // החזרת raw bytes בלבד
                return File(result, contentType);
            }
            catch (Exception ex)
            {
                // רישום השגיאה למערכת הניהול שגיאות
                _logger.LogError(ex, "שגיאה בהפקת דוח {ReportName}", request.ReportName);

                // בדיקה אם זו שגיאה מוכרת
                if (ex is ReportNotFoundException)
                {
                    return NotFound();
                }

                // החזרת שגיאה כללית - הEXE יבדוק לפי status code
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
        
        /// <summary>
        /// מאמת שקיים דוח בשם המבוקש ושהתבנית שלו זמינה
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
        /// <returns>תוצאת האימות</returns>
        [HttpGet("validate/{reportName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ValidateReport(string reportName, [FromQuery] string? departmentId)
        {
            _logger.LogInformation("התקבלה בקשה לאימות דוח {ReportName}, מחלקה: {DepartmentId}", 
                reportName, departmentId ?? "ברירת מחדל");

            try
            {
                // בדיקת תקינות המחלקה (אם התקבלה)
                if (!string.IsNullOrEmpty(departmentId) && !_fileSystemService.IsValidDepartment(departmentId))
                {
                    return BadRequest(new { error = $"מחלקה {departmentId} אינה תקפה" });
                }

                // בדיקה אם קיים דוח כזה בבסיס הנתונים
                var reportConfig = await _dataAccess.GetReportConfig(reportName);

                // בדיקה אם התבנית קיימת
                var templatePath = _fileSystemService.GetReportTemplatePath(reportName, departmentId);
                
                if (templatePath == null)
                {
                    return NotFound(new { 
                        error = $"תבנית לדוח {reportName} לא נמצאה", 
                        templateExists = false,
                        reportExists = true 
                    });
                }

                return Ok(new { 
                    isValid = true, 
                    message = $"הדוח {reportName} נמצא ותקין",
                    templatePath = templatePath,
                    reportConfig = new {
                        id = reportConfig.ReportID,
                        name = reportConfig.ReportName,
                        title = reportConfig.Title,
                        description = reportConfig.Description,
                        procedures = reportConfig.StoredProcName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה באימות דוח {ReportName}", reportName);
                
                if (ex.Message.Contains("not found") || ex is ReportNotFoundException)
                {
                    return NotFound(new { 
                        error = $"הדוח {reportName} לא נמצא במערכת",
                        reportExists = false
                    });
                }

                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "שגיאה באימות הדוח", details = ex.Message });
            }
        }
        
        /// <summary>
        /// מחזיר רשימת המחלקות הזמינות
        /// </summary>
        [HttpGet("departments")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetDepartments([FromServices] IConfiguration configuration)
        {
            var departments = configuration.GetSection("ReportSettings:DepartmentFolders:ValidDepartments")
                .Get<string[]>() ?? Array.Empty<string>();
                
            var enabled = configuration.GetValue<bool>("ReportSettings:DepartmentFolders:Enabled", false);
            
            return Ok(new { 
                enabled = enabled,
                departments = departments
            });
        }
    }
}
