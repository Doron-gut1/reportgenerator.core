//using Microsoft.Extensions.Options;
//using ReportGenerator.Api.Services;
//using ReportGenerator.Core.Configuration;
//using ReportGenerator.Core.Errors;
//using ReportGenerator.Core.Generators;


//namespace ReportGenerator.Api.Services
//{
//    /// <summary>
//    /// שירות לניהול התאמה בין HtmlTemplateManager לתמיכה במחלקות
//    /// </summary>
//    public class TemplateManagerAdapter
//    {
//        private readonly IFileSystemService _fileSystemService;
//        private readonly ILogger<TemplateManagerAdapter> _logger;
//        private readonly IOptions<ReportSettings> _settings;
//        private readonly bool _departmentFoldersEnabled;
//        private readonly HtmlTemplateManager _templateManager;

//        public TemplateManagerAdapter(
//            IFileSystemService fileSystemService,
//            ILogger<TemplateManagerAdapter> logger,
//            IOptions<ReportSettings> settings,
//            IConfiguration configuration)
//        {
//            _fileSystemService = fileSystemService;
//            _logger = logger;
//            _settings = settings;
//            _departmentFoldersEnabled = configuration.GetValue<bool>("ReportSettings:DepartmentFolders:Enabled", false);
            
//            // יצירת HtmlTemplateManager
//            _templateManager = new HtmlTemplateManager(settings);
//        }

//        /// <summary>
//        /// מתאים את הנתיב של התבנית לפי מחלקה
//        /// </summary>
//        /// <param name="reportName">שם הדוח</param>
//        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
//        public async Task<string> GetTemplateForReport(string reportName, string? departmentId)
//        {
//            // אם אין תמיכה במחלקות, משתמשים במנהל התבניות הרגיל
//            if (!_departmentFoldersEnabled || string.IsNullOrEmpty(departmentId))
//            {
//                try
//                {
//                    return await _templateManager.GetTemplateAsync(reportName);
//                }
//                catch (Exception ex)
//                {
//                    SimpleLogger.LogError( $"תבנית HTML לא נמצאה עבור דוח: {reportName}", ex);
//                    throw;
//                }
//            }

//            // בדיקת תקינות המחלקה
//            if (!_fileSystemService.IsValidDepartment(departmentId))
//            {
//                SimpleLogger.LogError($"מחלקה לא תקפה: {departmentId}");
//                throw new ArgumentException($"מחלקה לא תקפה: {departmentId}");
//            }

//            // קבלת הנתיב לתבנית לפי המחלקה
//            string? templatePath = _fileSystemService.GetReportTemplatePath(reportName, departmentId);
//            if (templatePath == null || !File.Exists(templatePath))
//            {
//                SimpleLogger.LogError( $"תבנית HTML לא נמצאה עבור דוח: {reportName} במחלקה: {departmentId}");
//                throw new FileNotFoundException($"תבנית לא נמצאה: {reportName} במחלקה: {departmentId}");
//            }

//            // קריאת התבנית מהקובץ
//            try
//            {
//                string templateHtml = await File.ReadAllTextAsync(templatePath);
//                return templateHtml;
//            }
//            catch (Exception ex)
//            {
//                SimpleLogger.LogError($"שגיאה בקריאת קובץ תבנית: {templatePath}", ex);
//                throw;
//            }
//        }
//    }
//}
