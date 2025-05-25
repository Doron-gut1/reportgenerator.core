using Microsoft.Extensions.Options;
using ReportGenerator.Core.Configuration;

namespace ReportGenerator.Api.Services
{
    /// <summary>
    /// ממשק לניהול מערכת הקבצים והרשאות
    /// </summary>
    public interface IFileSystemService
    {
        /// <summary>
        /// בדיקה אם קיימות הרשאות כתיבה לתיקייה
        /// </summary>
        /// <param name="path">נתיב לבדיקה</param>
        /// <returns>האם ההרשאות תקינות</returns>
        bool HasWritePermission(string path);

        /// <summary>
        /// קבלת נתיב תקין לתבנית HTML לפי שם דוח ומחלקה
        /// </summary>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
        /// <returns>נתיב מלא לתבנית, או null אם לא נמצא</returns>
        string? GetReportTemplatePath(string reportName, string? departmentId);

        /// <summary>
        /// שמירת קובץ פלט במיקום הרצוי
        /// </summary>
        /// <param name="content">תוכן הקובץ</param>
        /// <param name="reportName">שם הדוח</param>
        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
        /// <param name="format">פורמט הקובץ</param>
        /// <returns>נתיב מלא לקובץ שנשמר, או null אם הקובץ לא נשמר</returns>
        string? SaveOutputFile(byte[] content, string reportName, string? departmentId, string format);

        /// <summary>
        /// וידוא שתיקייה קיימת, וניסיון ליצור אותה אם לא
        /// </summary>
        /// <param name="path">הנתיב לוודא</param>
        /// <returns>האם התיקייה קיימת וניתנת לכתיבה</returns>
        bool EnsureDirectoryExists(string path);

        /// <summary>
        /// וידוא שתיקיית המחלקה תקינה
        /// </summary>
        /// <param name="departmentId">מזהה המחלקה</param>
        /// <returns>האם המחלקה תקינה</returns>
        bool IsValidDepartment(string? departmentId);
    }

    /// <summary>
    /// שירות לניהול מערכת הקבצים והרשאות
    /// </summary>
    public class FileSystemService : IFileSystemService
    {
        private readonly ILogger<FileSystemService> _logger;
        private readonly string _baseTemplatesFolder;
        private readonly string _outputFolder;
        private readonly bool _saveOutputFiles;
        private readonly bool _departmentFoldersEnabled;
        private readonly bool _createDepartmentFolderIfNotExists;
        private readonly string[] _validDepartments;

        public FileSystemService(IConfiguration configuration, ILogger<FileSystemService> logger)
        {
            _logger = logger;

            // טעינת הגדרות מקובץ התצורה
            _baseTemplatesFolder = configuration["ReportSettings:BaseTemplatesFolder"] ?? string.Empty;
            _outputFolder = configuration["ReportSettings:OutputFolder"] ?? string.Empty;
            _saveOutputFiles = configuration.GetValue<bool>("ReportSettings:SaveOutputFiles", false);
            _departmentFoldersEnabled = configuration.GetValue<bool>("ReportSettings:DepartmentFolders:Enabled", false);
            _createDepartmentFolderIfNotExists = configuration.GetValue<bool>("ReportSettings:DepartmentFolders:CreateIfNotExists", false);
            
            // טעינת רשימת מחלקות תקפות
            _validDepartments = configuration.GetSection("ReportSettings:DepartmentFolders:ValidDepartments")
                .Get<string[]>() ?? Array.Empty<string>();

            // בדיקת תקינות בסיסית של נתיבים
            if (string.IsNullOrEmpty(_baseTemplatesFolder))
            {
                _logger.LogWarning("נתיב תיקיית תבניות בסיסית לא הוגדר בקובץ התצורה");
            }

            if (_saveOutputFiles && string.IsNullOrEmpty(_outputFolder))
            {
                _logger.LogWarning("נתיב תיקיית פלט לא הוגדר בקובץ התצורה, אך שמירת קבצים מופעלת");
            }
        }

        /// <summary>
        /// בדיקה אם קיימות הרשאות כתיבה לתיקייה
        /// </summary>
        public bool HasWritePermission(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                // בדיקת קיום התיקייה
                if (!Directory.Exists(path))
                    return false;

                // בדיקת הרשאות כתיבה על ידי ניסיון ליצור קובץ זמני
                string testFile = Path.Combine(path, $"test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "Test");
                File.Delete(testFile);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "אין הרשאות כתיבה לתיקייה {Path}", path);
                return false;
            }
        }

        /// <summary>
        /// קבלת נתיב תקין לתבנית HTML לפי שם דוח ומחלקה
        /// </summary>
        public string? GetReportTemplatePath(string reportName, string? departmentId)
        {
            if (string.IsNullOrEmpty(_baseTemplatesFolder))
            {
                _logger.LogError("נתיב תיקיית תבניות בסיסית לא הוגדר");
                return null;
            }

            // בניית נתיב בסיסי
            string templatesFolder = _baseTemplatesFolder;

            // הוספת המחלקה לנתיב, אם צריך
            if (_departmentFoldersEnabled && !string.IsNullOrEmpty(departmentId))
            {
                // בדיקת תקינות המחלקה
                if (!IsValidDepartment(departmentId))
                {
                    _logger.LogWarning("מחלקה לא תקפה: {DepartmentId}", departmentId);
                    return null;
                }

                templatesFolder = Path.Combine(templatesFolder, departmentId);
            }

            // בדיקת קיום התיקייה
            if (!Directory.Exists(templatesFolder))
            {
                _logger.LogError("תיקיית תבניות לא קיימת: {Path}", templatesFolder);
                return null;
            }

            // בניית נתיב מלא לקובץ התבנית
            string templatePath = Path.Combine(templatesFolder, $"{reportName}.html");

            // בדיקת קיום הקובץ
            if (!File.Exists(templatePath))
            {
                _logger.LogError("קובץ תבנית לא קיים: {Path}", templatePath);
                return null;
            }

            return templatePath;
        }

        /// <summary>
        /// שמירת קובץ פלט במיקום הרצוי
        /// </summary>
        public string? SaveOutputFile(byte[] content, string reportName, string? departmentId, string format)
        {
            if (!_saveOutputFiles || string.IsNullOrEmpty(_outputFolder) || content == null || content.Length == 0)
                return null;

            try
            {
                // בניית נתיב תיקיית פלט
                string outputFolder = _outputFolder;

                // הוספת המחלקה לנתיב, אם צריך
                if (_departmentFoldersEnabled && !string.IsNullOrEmpty(departmentId))
                {
                    // בדיקת תקינות המחלקה
                    if (!IsValidDepartment(departmentId))
                    {
                        _logger.LogWarning("מחלקה לא תקפה: {DepartmentId}", departmentId);
                        return null;
                    }

                    outputFolder = Path.Combine(outputFolder, departmentId);
                }

                // וידוא קיום התיקייה
                if (!EnsureDirectoryExists(outputFolder))
                {
                    _logger.LogError("לא ניתן ליצור תיקיית פלט: {Path}", outputFolder);
                    return null;
                }

                // קביעת שם הקובץ
                string fileExtension = format.ToLower() == "pdf" ? "pdf" : "xlsx";
                string fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}.{fileExtension}";
                string filePath = Path.Combine(outputFolder, fileName);

                // שמירת הקובץ
                File.WriteAllBytes(filePath, content);
                _logger.LogInformation("הקובץ נשמר בהצלחה: {Path}", filePath);

                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בשמירת קובץ הפלט");
                return null;
            }
        }

        /// <summary>
        /// וידוא שתיקייה קיימת, וניסיון ליצור אותה אם לא
        /// </summary>
        public bool EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                // אם התיקייה כבר קיימת
                if (Directory.Exists(path))
                {
                    // בדיקת הרשאות כתיבה
                    return HasWritePermission(path);
                }

                // ניסיון ליצור את התיקייה
                Directory.CreateDirectory(path);
                _logger.LogInformation("נוצרה תיקייה חדשה: {Path}", path);

                // בדיקת הרשאות לאחר היצירה
                return HasWritePermission(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה ביצירת תיקייה: {Path}", path);
                return false;
            }
        }

        /// <summary>
        /// וידוא שתיקיית המחלקה תקינה
        /// </summary>
        public bool IsValidDepartment(string? departmentId)
        {
            if (string.IsNullOrEmpty(departmentId))
                return true; // מחלקה ריקה תקפה (ברירת מחדל)

            // אם יש רשימה מוגדרת, יש לוודא שהמחלקה ברשימה
            if (_validDepartments.Length > 0)
            {
                return _validDepartments.Contains(departmentId, StringComparer.OrdinalIgnoreCase);
            }

            // אם אין רשימה מוגדרת, כל מחלקה תקפה
            return true;
        }
    }
}
