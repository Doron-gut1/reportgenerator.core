﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Errors;
using ReportGenerator.Core.Interfaces;

namespace ReportGenerator.Core.Generators
{
    /// <summary>
    /// מנהל תבניות HTML - אחראי על טעינה, שמירה וניהול של קבצי תבניות HTML
    /// </summary>
    public class HtmlTemplateManager : ITemplateManager
    {
        private readonly string _templatesFolder;
        private readonly IErrorManager _errorManager;

        /// <summary>
        /// יוצר מופע חדש של מנהל התבניות
        /// </summary>
        /// <param name="templatesFolder">נתיב לתיקיית התבניות</param>
        /// <param name="errorManager">מנהל שגיאות</param>
        public HtmlTemplateManager(IOptions<ReportSettings> settings, IErrorManager errorManager)
        {
            _errorManager = errorManager ?? throw new ArgumentNullException(nameof(errorManager));

            if (settings?.Value == null)
            {
                var error = new ArgumentNullException(nameof(settings));
                _errorManager.LogError(
                    ErrorCode.Template_Invalid_Format,
                    ErrorSeverity.Critical,
                    "הגדרות דוחות לא יכולות להיות ריקות",
                    error);
                throw error;
            }

            if (string.IsNullOrEmpty(settings.Value.TemplatesFolder))
            {
                var error = new ArgumentException("Template folder path cannot be empty", nameof(settings));
                _errorManager.LogError(
                    ErrorCode.Template_Invalid_Format,
                    ErrorSeverity.Critical,
                    "נתיב תיקיית תבניות לא יכול להיות ריק",
                    error);
                throw error;
            }

            _templatesFolder = settings.Value.TemplatesFolder;

            // וידוא שהתיקייה קיימת
            try
            {
                if (!Directory.Exists(_templatesFolder))
                {
                    Directory.CreateDirectory(_templatesFolder);
                    _errorManager.LogInfo(
                        ErrorCode.General_Error,
                        $"נוצרה תיקייה חדשה לתבניות: {_templatesFolder}");
                }
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.Template_Not_Found,
                    ErrorSeverity.Critical,
                    $"לא ניתן ליצור את תיקיית התבניות: {_templatesFolder}",
                    ex);
                throw new Exception($"Cannot create templates directory at {_templatesFolder}", ex);
            }
        }

        /// <summary>
        /// בודק אם תבנית קיימת
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>האם התבנית קיימת</returns>
        public bool TemplateExists(string templateName)
        {
            try
            {
                string fullPath = GetTemplatePath(templateName);
                return File.Exists(fullPath);
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.Template_Not_Found,
                    ErrorSeverity.Error,
                    $"שגיאה בבדיקת קיום תבנית {templateName}",
                    ex);
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
                _errorManager.LogError(
                    ErrorCode.Template_Not_Found,
                    ErrorSeverity.Error,
                    $"שגיאה בקבלת רשימת תבניות זמינות מהתיקייה {_templatesFolder}",
                    ex);
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// טוען תבנית HTML מהדיסק
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>תוכן התבנית כמחרוזת</returns>
        public async Task<string> GetTemplateAsync(string templateName)
        {
            string fullPath = GetTemplatePath(templateName);

            try
            {
                if (!File.Exists(fullPath))
                {
                    var error = new FileNotFoundException($"Template '{templateName}' not found at {fullPath}");
                    _errorManager.LogError(
                        ErrorCode.Template_Not_Found,
                        ErrorSeverity.Critical,
                        $"תבנית '{templateName}' לא נמצאה בנתיב {fullPath}",
                        error,
                        reportName: templateName);
                    throw error;
                }

                string templateContent = await File.ReadAllTextAsync(fullPath);
                
                if (string.IsNullOrWhiteSpace(templateContent))
                {
                    _errorManager.LogWarning(
                        ErrorCode.Template_Invalid_Format,
                        $"תבנית '{templateName}' ריקה או מכילה רווחים בלבד",
                        reportName: templateName);
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
                _errorManager.LogError(
                    ErrorCode.Template_Invalid_Format,
                    ErrorSeverity.Critical,
                    $"שגיאה בקריאת קובץ תבנית {templateName}",
                    ex,
                    reportName: templateName);
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
                _errorManager.LogInfo(
                    ErrorCode.General_Error,
                    $"תבנית {templateName} נשמרה בהצלחה");
            }
            catch (Exception ex)
            {
                _errorManager.LogError(
                    ErrorCode.Template_Processing_Failed,
                    ErrorSeverity.Error,
                    $"שגיאה בשמירת תבנית {templateName}",
                    ex,
                    reportName: templateName);
                throw new Exception($"Failed to save template {templateName}", ex);
            }
        }

        /// <summary>
        /// מקבל את הנתיב המלא לקובץ התבנית
        /// </summary>
        /// <param name="templateName">שם התבנית (ללא סיומת)</param>
        /// <returns>נתיב מלא לקובץ</returns>
        private string GetTemplatePath(string templateName)
        {
            if (string.IsNullOrEmpty(templateName))
            {
                var error = new ArgumentException("Template name cannot be null or empty", nameof(templateName));
                _errorManager.LogError(
                    ErrorCode.Template_Invalid_Format,
                    ErrorSeverity.Error,
                    "שם תבנית לא יכול להיות ריק",
                    error);
                throw error;
            }
            
            // ניקוי שם הקובץ משמות תווים אסורים
            string safeFileName = string.Join("_", templateName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_templatesFolder, $"{safeFileName}.html");
        }
    }
}
