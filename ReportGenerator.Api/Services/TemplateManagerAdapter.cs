using Microsoft.Extensions.Options;
using ReportGenerator.Api.Services;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;

namespace ReportGenerator.Api.Services
{
    /// <summary>
    /// שירות לניהול התאמה בין HtmlTemplateManager לתמיכה במחלקות
    /// </summary>
    public class TemplateManagerAdapter
    {
        private readonly ITemplateManager _templateManager;
        private readonly IFileSystemService _fileSystemService;
        private readonly IErrorManager _errorManager;
        private readonly ILogger<TemplateManagerAdapter> _logger;
        private readonly IOptions<ReportSettings> _settings;
        private readonly bool _departmentFoldersEnabled;

        public TemplateManagerAdapter(
            ITemplateManager templateManager,
            IFileSystemService fileSystemService,
            IErrorManager errorManager,
            ILogger<TemplateManagerAdapter> logger,
            IOptions<ReportSettings> settings,
            IConfiguration configuration)
        {
            _templateManager = templateManager;
            _fileSystemService = fileSystemService;
            _errorManager = errorManager;
            _logger = logger;
            _settings = settings;
            _departmentFoldersEnabled = configuration.GetValue<bool>("ReportSettings:DepartmentFolders:Enabled", false);
        }

        /// <summary>
        /// מתאים את הנתיב של התבנית לפי מחלקה
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
        public async Task<string> GetTemplateForReport(string reportName, string? departmentId)
        {
            // אם אין תמיכה במחלקות, משתמשים במנהל התבניות הרגיל
            if (!_departmentFoldersEnabled || string.IsNullOrEmpty(departmentId))
            {
                try
                {
                    return await _templateManager.GetTemplateAsync(reportName);
                }
                catch (Exception ex)
                {
                    _errorManager.LogError(
                        ErrorCode.Template_Not_Found,
                        ErrorSeverity.Critical,
                        $"תבנית HTML לא נמצאה עבור דוח: {reportName}",
                        ex);
                    throw;
                }
            }

            // בדיקת תקינות המחלקה
            if (!_fileSystemService.IsValidDepartment(departmentId))
            {
                _errorManager.LogError(
                    ErrorCode.Template_Invalid_Department,
                    ErrorSeverity.Critical,
                    $"מחלקה לא תקפה: {departmentId}");
                throw new ArgumentException($"מחלקה לא תקפה: {departmentId}");
            }

            // קבלת הנתיב לתבנית לפי המחלקה
            string? templatePath = _fileSystemService.GetReportTemplatePath(reportName, departmentId);
            if (templatePath == null || !File.Exists(templatePath))
            {
                _errorManager.LogError(
                    ErrorCode.Template_Not_Found,
                    ErrorSeverity.Critical,
                    $"תבנית HTML לא נמצאה עבור דוח: {reportName} במחלקה: {departmentId}");
                throw new FileNotFoundException($"תבנית לא נמצאה: {reportName} במחלקה: {departmentId}");
            }

            // קריאת התבנית מהקובץ
            try
            {
                string templateHtml = await File.ReadAllTextAsync(templatePath);
                return templateHtml;
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.Template_Reading_Failed,
                    ErrorSeverity.Critical,
                    $"שגיאה בקריאת קובץ תבנית: {templatePath}",
                    ex);
                throw;
            }
        }
    }
}
