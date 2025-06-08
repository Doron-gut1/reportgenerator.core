using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Errors;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מנהל תבניות HTML - אחראי על טעינה, שמירה וניהול של קבצי תבניות HTML
    /// </summary>
    public class HtmlTemplateManager
    {
        private  string _templatesFolder;

        /// <summary>
        /// יוצר מופע חדש של מנהל התבניות
        /// </summary>
        /// <param name="settings">הגדרות דוחות</param>
        /// <param name="errorManager">מנהל שגיאות</param>
        public HtmlTemplateManager(IOptions<ReportSettings> settings)
        {
            InitializeTemplateManager(settings?.Value);
        }

        /// <summary>
        /// יוצר מופע חדש של מנהל התבניות
        /// </summary>
        /// <param name="templatesFolder">נתיב לתיקיית התבניות</param>
        /// <param name="errorManager">מנהל שגיאות</param>
        public HtmlTemplateManager(string templatesFolder)
        {
            var settings = new ReportSettings { TemplatesFolder = templatesFolder };
            InitializeTemplateManager(settings);
        }

        /// <summary>  
        /// אתחול מנהל התבניות  
        /// </summary>  
        private void InitializeTemplateManager(ReportSettings settings)
        {
            if (settings == null)
            {
                var error = new ArgumentNullException(nameof(settings));
                SimpleLogger.LogCriticalError(
                    "הגדרות דוחות לא יכולות להיות ריקות",
                    error);
                throw error;
            }

            if (string.IsNullOrEmpty(settings.TemplatesFolder))
            {
                var error = new ArgumentException("Template folder path cannot be empty", nameof(settings));
                SimpleLogger.LogError(
                    "נתיב תיקיית תבניות לא יכול להיות ריק",
                    error);
                throw error;
            }

            _templatesFolder = settings.TemplatesFolder;

            // וידוא שהתיקייה קיימת  
            try
            {
                if (!Directory.Exists(_templatesFolder))
                {
                    Directory.CreateDirectory(_templatesFolder);
                    SimpleLogger.LogInfo( $"נוצרה תיקייה חדשה לתבניות: {_templatesFolder}");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError($"לא ניתן ליצור את תיקיית התבניות: {_templatesFolder}",
                    ex);
                throw new Exception($"Cannot create templates directory at {_templatesFolder}", ex);
            }
        }

        /// <summary>
        /// בודק אם תבנית קיימת
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>האם התבנית קיימת</returns>
        public bool TemplateExists(string templateName, string? departmentId = null)
        {
        try
        {
        string fullPath = GetTemplatePath(templateName, departmentId);
        return File.Exists(fullPath);
        }
        catch (Exception ex)
        {
                SimpleLogger.LogError($"שגיאה בבדיקת קיום תבנית {templateName}",ex);
        return false;
        }
        }

        /// <summary>
        /// מקבל רשימה של כל התבניות הזמינות
        /// </summary>
        /// <returns>רשימת שמות תבניות (ללא סיומת)</returns>
        public IEnumerable<string> GetAvailableTemplates()
        {
            try
            {
                return Directory.GetFiles(_templatesFolder, "*.html")
                    .Select(file => Path.GetFileNameWithoutExtension(file));
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError($"שגיאה בקבלת רשימת תבניות זמינות מהתיקייה {_templatesFolder}",ex);
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// טוען תבנית HTML מהדיסק
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
        /// <returns>תוכן התבנית כמחרוזת</returns>
        public async Task<string> GetTemplateAsync(string templateName, string? departmentId = null)
        {
        string fullPath = GetTemplatePath(templateName, departmentId);

        try
        {
        if (!File.Exists(fullPath))
        {
        var error = new FileNotFoundException($"Template '{templateName}' not found at {fullPath}");
                    SimpleLogger.LogError($"תבנית '{templateName}' לא נמצאה בנתיב {fullPath}",error,reportName: templateName);
            throw error;
            }

        string templateContent = await File.ReadAllTextAsync(fullPath);
        
        if (string.IsNullOrWhiteSpace(templateContent))
        {
                    SimpleLogger.LogWarning($"תבנית '{templateName}' ריקה או מכילה רווחים בלבד",reportName: templateName);
        }
        
            return templateContent;
        }
        catch (FileNotFoundException)
        {
        // כבר טופל למעלה
            throw;
        }
        catch (Exception ex)
        {
            var error = new Exception($"Failed to read template file {fullPath}", ex);
            SimpleLogger.LogError($"שגיאה בקריאת קובץ תבנית {templateName}",ex,reportName: templateName);
                throw error;
         }
    }

        /// <summary>
        /// שומר תבנית HTML לדיסק
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <param name="content">תוכן התבנית</param>
        public async Task SaveTemplateAsync(string templateName, string content)
        {
            string fullPath = GetTemplatePath(templateName);
            
            try
            {
                await File.WriteAllTextAsync(fullPath, content);
                SimpleLogger.LogInfo( $"תבנית {templateName} נשמרה בהצלחה");
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError($"שגיאה בשמירת תבנית {templateName}", ex, reportName: templateName);
                throw new Exception($"Failed to save template {templateName}", ex);
            }
        }

        /// <summary>
        /// מקבל את הנתיב המלא לקובץ התבנית
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <param name="departmentId">מזהה המחלקה (אופציונלי)</param>
        /// <returns>נתיב מלא לקובץ</returns>
        private string GetTemplatePath(string templateName, string? departmentId = null)
        {
            if (string.IsNullOrEmpty(templateName))
            {
                var error = new ArgumentException("Template name cannot be null or empty", nameof(templateName));
                SimpleLogger.LogError("שם תבנית לא יכול להיות ריק",error);
                throw error;
            }
            
            // ניקוי שם הקובץ משמות תווים אסורים
            string safeFileName = string.Join("_", templateName.Split(Path.GetInvalidFileNameChars()));
            
            // בדיקה אם צריך להשתמש בתיקיית מחלקה
            string baseFolder = _templatesFolder;
            if (!string.IsNullOrEmpty(departmentId))
            {
                // בדיקת המחלקה לתווים לא חוקיים
                string safeDepartmentId = string.Join("_", departmentId.Split(Path.GetInvalidFileNameChars()));
                baseFolder = Path.Combine(baseFolder, safeDepartmentId);
                
                // בדיקה אם התיקייה קיימת
                if (!Directory.Exists(baseFolder))
                {
                    SimpleLogger.LogWarning($"תיקיית מחלקה '{departmentId}' לא קיימת. מנסה ליצור אותה.");
                    
                    // ניסיון ליצור את התיקייה
                    try
                    {
                        Directory.CreateDirectory(baseFolder);
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError($"לא ניתן ליצור את תיקיית המחלקה: {departmentId}", ex);
                        // אם לא הצליח ליצור את התיקייה, נחזור לתיקייה הראשית
                        baseFolder = _templatesFolder;
                    }
                }
            }
            
            return Path.Combine(baseFolder, $"{safeFileName}.html");
        }
    }
}
